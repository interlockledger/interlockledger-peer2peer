// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2022 InterlockLedger Network
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met
//
// * Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// * Neither the name of the copyright holder nor the names of its
//   contributors may be used to endorse or promote products derived from
//   this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES, LOSS OF USE, DATA, OR PROFITS, OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// ******************************************************************************************************************************

namespace InterlockLedger.Peer2Peer
{
    public class MessageParser
    {
        public MessageParser(ulong expectedTag, ulong livenessMessageTag, ILogger logger, Func<NetworkMessageSlice, Task<Success>> messageProcessor, Func<ulong, Task> livenessProcessorAsync) {
            _messageProcessor = messageProcessor.Required();
            _livenessProcessorAsync = livenessProcessorAsync.Required();
            _logger = logger.Required();
            _expectedTag = expectedTag;
            _livenessMessageTag = livenessMessageTag;
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

            SequencePosition InnerParse(ReadOnlySequence<byte> buffer) {
                long current = 0;
                if (!buffer.IsEmpty)
                    while (current < buffer.Length) {
                        switch (_state) {
                        case State.Init:
                            Step_Initialize();
                            break;

                        case State.ReadTag:
                            AfterReadingILIntDo(buffer, ref current, _tagReader, Step_CheckTag);
                            break;

                        case State.ReadLength:
                            AfterReadingILIntDo(buffer, ref current, _lengthReader, Step_CheckLengthToRead);
                            break;

                        case State.ReadBytes:
                            (_lengthToRead, _body) = ReadBytes(buffer, ref current, _lengthToRead, _body);
                            _state = _lengthToRead != 0 ? State.ReadBytes : State.ReadChannel;
                            break;

                        case State.SkipBytes:
                            _lengthToRead = SkipBytes(buffer, ref current, _lengthToRead);
                            _state = _lengthToRead != 0 ? State.SkipBytes : State.SkipChannel;
                            break;

                        case State.ReadChannel:
                            AfterReadingILIntDo(buffer, ref current, _channelReader, Step_ProcessMessageFor);
                            break;

                        case State.SkipChannel:
                            AfterReadingILIntDo(buffer, ref current, _channelReader, (_) => _state = State.Init);
                            break;

                        case State.ProcessLiveness:
                            AfterReadingILIntDo(buffer, ref current, _livenessReader, Step_ProcessLivenessMessage);
                            break;
                        }
                    }
                _body = _body.Realloc();
                return buffer.End;
            }
        }

        private const ulong _maxBytesToRead = 64 * 1024 * 1024;
        private readonly ILIntReader _channelReader = new();
        private readonly ulong _expectedTag;
        private readonly ILIntReader _lengthReader = new();
        private readonly ulong _livenessMessageTag;
        private readonly Func<ulong, Task> _livenessProcessorAsync;
        private readonly ILIntReader _livenessReader = new();
        private readonly ILogger _logger;
        private readonly Func<NetworkMessageSlice, Task<Success>> _messageProcessor;
        private readonly ILIntReader _tagReader = new();
        private ReadOnlySequence<byte> _body = ReadOnlySequence<byte>.Empty;
        private ulong _lengthToRead;
        private volatile int _parsingCount = 0;
        private State _state = State.Init;

        private enum State
        {
            Init,
            ReadTag,
            ReadLength,
            ReadBytes,
            ReadChannel,
            SkipBytes,
            SkipChannel,
            ProcessLiveness
        }

        private static void AfterReadingILIntDo(ReadOnlySequence<byte> buffer, ref long current, ILIntReader reader, Action<ulong> action) {
            var nextByte = buffer.Slice(current++, 1).First.Span[0];
            if (reader.Done(nextByte))
                action(reader.Value);
        }

        private static string BuildMessageBodyReceived(ulong channel, ReadOnlySequence<byte> body)
            => $"Message body received on channel {channel}: {body.ToUrlSafeBase64()}";

        private static (ulong, ReadOnlySequence<byte>) ReadBytes(ReadOnlySequence<byte> buffer, ref long current, ulong length, ReadOnlySequence<byte> body) {
            long bytesToRead = Math.Min((long)length, buffer.Length - current);
            body = body.Add(buffer.Slice(current, bytesToRead));
            current += bytesToRead;
            return (length - (ulong)bytesToRead, body);
        }

        private static ulong SkipBytes(ReadOnlySequence<byte> buffer, ref long current, ulong length) {
            long bytesToRead = Math.Min((long)length, buffer.Length - current);
            current += bytesToRead;
            return length - (ulong)bytesToRead;
        }

        private void LogTrace(string logMessage) => LogTrace(() => logMessage);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Nope")]
        private void LogTrace(Func<string> buildLogMessage) {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace(buildLogMessage.Required()());
        }

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
            if (tag == _expectedTag) {
                LogTrace("Receiving new message");
                _state = State.ReadLength;
            } else if (tag == _livenessMessageTag) {
                LogTrace("Receiving new liveness message");
                _state = State.ProcessLiveness;
            } else {
                LogTrace($"Ignoring bad tag {tag}");
                _state = State.Init;
            }
        }

        private void Step_Initialize() {
            _lengthToRead = 0;
            _tagReader.Reset();
            _lengthReader.Reset();
            _channelReader.Reset();
            _livenessReader.Reset();
            _body = ReadOnlySequence<byte>.Empty;
            _state = State.ReadTag;
            LastResult = Success.Next;
        }

        private void Step_ProcessLivenessMessage(ulong liveness) {
            try {
                _livenessProcessorAsync(liveness);
            } catch (Exception e) {
                _logger.LogError(e, "Failed to process last liveness message!");
            } finally {
                _state = State.Init;
            }
        }

        private void Step_ProcessMessageFor(ulong channel) {
            try {
                if (!_body.IsEmpty) {
                    LogTrace(() => BuildMessageBodyReceived(channel, _body));
                    LastResult = ProcessMessage(new NetworkMessageSlice(channel, _body)).Result;
                }
            } catch (Exception e) {
                _logger.LogError(e, "Failed to process last message!");
            } finally {
                _state = State.Init;
            }

            async Task<Success> ProcessMessage(NetworkMessageSlice messageSlice)
                => await _messageProcessor(messageSlice).ConfigureAwait(false);
        }
    }
}