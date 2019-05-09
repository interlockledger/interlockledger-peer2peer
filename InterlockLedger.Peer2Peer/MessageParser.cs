/******************************************************************************************************************************

Copyright (c) 2018-2019 InterlockLedger Network
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

using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class MessageParser
    {
        public MessageParser(ulong expectedTag, ILogger logger, Func<ChannelBytes, Task<Success>> messageProcessor) {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _expectedTag = expectedTag;
            LastResult = Success.Next;
            _state = State.Init;
        }

        public Success LastResult { get; private set; }

        public SequencePosition Parse(ReadOnlySequence<byte> buffer) {
            try {
                if (Interlocked.Increment(ref _parsingCount) > 1)
                    throw new InvalidOperationException("This MessageParser is already parsing a buffer!");
                return InnerParse(buffer);
            } finally {
                Interlocked.Decrement(ref _parsingCount);
            }
        }

        private const ulong _maxBytesToRead = 16 * 1024 * 1024;
        private readonly ILIntReader _channelReader = new ILIntReader();
        private readonly ulong _expectedTag;
        private readonly ILIntReader _lengthReader = new ILIntReader();
        private readonly ILogger _logger;
        private readonly Func<ChannelBytes, Task<Success>> _messageProcessor;
        private readonly List<ReadOnlyMemory<byte>> _segments = new List<ReadOnlyMemory<byte>>();
        private readonly ILIntReader _tagReader = new ILIntReader();
        private ulong _channel;
        private ulong _lengthToRead;
        private volatile int _parsingCount = 0;
        private State _state = State.Init;

        private SequencePosition InnerParse(ReadOnlySequence<byte> buffer) {
            long current = 0;
            ulong ReadBytes(ulong length, bool skip) {
                long bytesToRead = Math.Min((long)length, buffer.Length - current);
                if (!skip) {
                    var slice = buffer.Slice(current, bytesToRead);
                    foreach (var segment in slice)
                        _segments.Add(segment.ToArray());
                }
                current += bytesToRead;
                return length - (ulong)bytesToRead;
            }
            byte ReadNextByte() => buffer.Slice(current++, 1).First.Span[0];
            if (!buffer.IsEmpty)
                while (current < buffer.Length) {
                    switch (_state) {
                    case State.Init:
                        _lengthToRead = 0;
                        _tagReader.Reset();
                        _lengthReader.Reset();
                        _channelReader.Reset();
                        _segments.Clear();
                        _state = State.ReadTag;
                        LastResult = Success.Next;
                        break;

                    case State.ReadTag:
                        if (_tagReader.Done(ReadNextByte())) {
                            var tag = _tagReader.Value;
                            if (tag != _expectedTag) {
                                _logger.LogWarning($"Ignoring bad tag {tag}");
                                _state = State.Init;
                                break;
                            }
                            _logger.LogTrace("Receiving new message");
                            _state = State.ReadLength;
                        }
                        break;

                    case State.ReadLength:
                        if (_lengthReader.Done(ReadNextByte())) {
                            _lengthToRead = _lengthReader.Value;
                            if (_lengthToRead > _maxBytesToRead) {
                                _logger.LogTrace($"Skipping {_lengthToRead} bytes for message body, too long");
                                _state = State.SkipBytes;
                            } else {
                                _logger.LogTrace($"Expecting {_lengthToRead} bytes for message body");
                                _state = _lengthToRead == 0 ? State.ReadChannel : State.ReadBytes;
                            }
                        }
                        break;

                    case State.ReadBytes:
                        _lengthToRead = ReadBytes(_lengthToRead, skip: false);
                        _state = _lengthToRead != 0 ? State.ReadBytes : State.ReadChannel;
                        break;

                    case State.SkipBytes:
                        _lengthToRead = ReadBytes(_lengthToRead, skip: true);
                        _state = _lengthToRead != 0 ? State.SkipBytes : State.SkipChannel;
                        break;

                    case State.ReadChannel:
                        if (_channelReader.Done(ReadNextByte())) {
                            _channel = _channelReader.Value;
                            try {
                                if (_segments.Any()) {
                                    _logger.LogTrace($"Message body received {string.Join("|", _segments.Select(b => b.ToBase64()))}");
                                    LastResult = _messageProcessor(new ChannelBytes(_channel, _segments)).Result;
                                }
                            } catch (Exception e) {
                                _logger.LogError(e, "Failed to process last message!");
                            } finally {
                                _state = State.Init;
                            }
                        }
                        break;

                    case State.SkipChannel:
                        if (_channelReader.Done(ReadNextByte())) {
                            _state = State.Init;
                        }
                        break;

                    default:
                        break;
                    }
                }
            return buffer.End;
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