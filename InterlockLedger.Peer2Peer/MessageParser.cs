/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InterlockLedger.Peer2Peer
{
    public class MessageParser
    {
        public MessageParser(ulong expectedTag, IMessageProcessor messageProcessor, ILogger logger) {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _expectedTag = expectedTag;
            LastResult = Success.None;
        }

        public bool Continue =>
            _messageProcessor.AwaitMultipleAnswers ? !LastResult.HasFlag(Success.Exit) : !LastResult.HasFlag(Success.Processed);

        public Success LastResult { get; private set; }

        public SequencePosition Parse(ReadOnlySequence<byte> buffer) {
            long current = 0;
            void Process() {
                if (_lengthToRead == 0) {
                    _logger.LogTrace($"Message body received {_segments.Select(b => b.ToBase64())}");
                    LastResult = _messageProcessor.Process(_segments);
                    _state = State.Init;
                } else {
                    _state = State.ReadBytes;
                }
            }
            ulong ReadBytes(ulong length) {
                if (length >= int.MaxValue)
                    throw new InvalidOperationException("Too many bytes to read!");
                long bytesToRead = Math.Min((long)length, buffer.Length - current);
                var slice = buffer.Slice(current, bytesToRead);
                foreach (var segment in slice)
                    _segments.Add(segment);
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
                        _segments.Clear();
                        _state = State.ReadTag;
                        LastResult = Success.None;
                        break;

                    case State.ReadTag:
                        if (_tagReader.Done(ReadNextByte())) {
                            var tag = _tagReader.Value;
                            if (tag != _expectedTag)
                                throw new InvalidDataException($"Invalid message tag {tag}");
                            _logger.LogTrace("Receiving new message");
                            _state = State.ReadLength;
                        }
                        break;

                    case State.ReadLength:
                        if (_lengthReader.Done(ReadNextByte())) {
                            _lengthToRead = _lengthReader.Value;
                            _logger.LogTrace($"Expecting {_lengthReader} bytes for message body");
                            Process();
                        }
                        break;

                    case State.ReadBytes:
                        _lengthToRead = ReadBytes(_lengthToRead);
                        Process();
                        break;
                    }
                }
            return buffer.End;
        }

        private readonly ulong _expectedTag;
        private readonly ILIntReader _lengthReader = new ILIntReader();
        private readonly ILogger _logger;
        private readonly IMessageProcessor _messageProcessor;
        private readonly List<ReadOnlyMemory<byte>> _segments = new List<ReadOnlyMemory<byte>>();
        private readonly ILIntReader _tagReader = new ILIntReader();
        private ulong _lengthToRead;
        private State _state = State.Init;

        private enum State
        {
            Init,
            ReadTag,
            ReadLength,
            ReadBytes
        }
    }
}