/******************************************************************************************************************************

Copyright (c) 2018-2020 InterlockLedger Network
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of the copyright holder nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

******************************************************************************************************************************/

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InterlockLedger.Tags;
using Microsoft.Extensions.Logging;

namespace InterlockLedger.Peer2Peer
{
    public class MessageParser
    {
        public MessageParser(ulong expectedTag, ILogger logger, Func<NetworkMessageSlice, Task<Success>> messageProcessor) {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _expectedTag = expectedTag;
            LastResult = Success.Next;
            _state = State.Init;
        }

        public Success LastResult { get; private set; }

        public SequencePosition Parse(ReadOnlySequence<byte> buffer) {
            try {
                return Interlocked.Increment(ref _parsingCount) > 1
                    ? throw new InvalidOperationException("This MessageParser is already parsing a buffer!")
                    : InnerParse(buffer);
            } finally {
                Interlocked.Decrement(ref _parsingCount);
            }
        }

        private const ulong _maxBytesToRead = 16 * 1024 * 1024;
        private readonly ILIntReader _channelReader = new ILIntReader();
        private readonly ulong _expectedTag;
        private readonly ILIntReader _lengthReader = new ILIntReader();
        private readonly ILogger _logger;
        private readonly Func<NetworkMessageSlice, Task<Success>> _messageProcessor;
        private readonly List<ReadOnlyMemory<byte>> _segments = new List<ReadOnlyMemory<byte>>();
        private readonly ILIntReader _tagReader = new ILIntReader();
        private ulong _lengthToRead;
        private volatile int _parsingCount = 0;
        private State _state = State.Init;

        private static void AfterReadingILintDo(ReadOnlySequence<byte> buffer, ref long current, ILIntReader reader, Action<ulong> action) {
            var nextByte = buffer.Slice(current++, 1).First.Span[0];
            if (reader.Done(nextByte))
                action(reader.Value);
        }

        private static string BuildMessageBodyReceived(ulong channel, IEnumerable<ReadOnlyMemory<byte>> segments)
            => $"Message body received on channel {channel}: {string.Join("|", segments.Select(b => b.Slice(0, 90).ToBase64()))}";

        private static ulong ReadBytes(ReadOnlySequence<byte> buffer, ref long current, ulong length, List<ReadOnlyMemory<byte>> segments = null) {
            long bytesToRead = Math.Min((long)length, buffer.Length - current);
            if (segments != null) {
                var slice = buffer.Slice(current, bytesToRead);
                foreach (var segment in slice) {
                    segments.Add(segment.ToArray());
                }
            }
            current += bytesToRead;
            return length - (ulong)bytesToRead;
        }

        private SequencePosition InnerParse(ReadOnlySequence<byte> buffer) {
            long current = 0;
            if (!buffer.IsEmpty)
                while (current < buffer.Length) {
                    switch (_state) {
                    case State.Init:
                        Step_Initialize();
                        break;

                    case State.ReadTag:
                        AfterReadingILintDo(buffer, ref current, _tagReader, Step_CheckTag);
                        break;

                    case State.ReadLength:
                        AfterReadingILintDo(buffer, ref current, _lengthReader, Step_CheckLengthToRead);
                        break;

                    case State.ReadBytes:
                        _lengthToRead = ReadBytes(buffer, ref current, _lengthToRead, _segments);
                        _state = _lengthToRead != 0 ? State.ReadBytes : State.ReadChannel;
                        break;

                    case State.SkipBytes:
                        _lengthToRead = ReadBytes(buffer, ref current, _lengthToRead);
                        _state = _lengthToRead != 0 ? State.SkipBytes : State.SkipChannel;
                        break;

                    case State.ReadChannel:
                        AfterReadingILintDo(buffer, ref current, _channelReader, Step_ProcessMessageFor);
                        break;

                    case State.SkipChannel:
                        AfterReadingILintDo(buffer, ref current, _channelReader, (_) => _state = State.Init);
                        break;
                    }
                }
            return buffer.End;
        }

        private void LogTrace(string logMessage) => LogTrace(() => logMessage);

        private void LogTrace(Func<string> buildLogMessage) {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace(buildLogMessage());
        }

        private async Task<Success> ProcessMessage(NetworkMessageSlice messageSlice) => await _messageProcessor(messageSlice).ConfigureAwait(false);

        private void Step_CheckLengthToRead(ulong lengthToRead) {
            _lengthToRead = lengthToRead;
            if (_lengthToRead > _maxBytesToRead) {
                LogTrace($"Skipping {_lengthToRead} bytes for message body, too long");
                _state = State.SkipBytes;
                // TODO some splitting of large payloads to avoid having them on memory
            } else {
                LogTrace($"Expecting {_lengthToRead} bytes for message body");
                _state = _lengthToRead == 0 ? State.ReadChannel : State.ReadBytes;
            }
        }

        private void Step_CheckTag(ulong tag) {
            if (tag != _expectedTag) {
                LogTrace($"Ignoring bad tag {tag}");
                _state = State.Init;
            } else {
                LogTrace("Receiving new message");
                _state = State.ReadLength;
            }
        }

        private void Step_Initialize() {
            _lengthToRead = 0;
            _tagReader.Reset();
            _lengthReader.Reset();
            _channelReader.Reset();
            _segments.Clear();
            _state = State.ReadTag;
            LastResult = Success.Next;
        }

        private void Step_ProcessMessageFor(ulong channel) {
            try {
                if (_segments.Count > 0) {
                    LogTrace(() => BuildMessageBodyReceived(channel, _segments));
                    LastResult = ProcessMessage(new NetworkMessageSlice(channel, _segments)).Result;
                }
            } catch (Exception e) {
                _logger.LogError(e, "Failed to process last message!");
            } finally {
                _state = State.Init;
            }
        }

        private enum State
        {
            Init,
            ReadTag,
            ReadLength,
            ReadBytes,
            ReadChannel,
            SkipBytes,
            SkipChannel
        }
    }
}