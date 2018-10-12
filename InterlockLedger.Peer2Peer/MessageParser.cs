/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace InterlockLedger.Peer2Peer
{
    public class MessageParser
    {
        public MessageParser(ulong expectedTag, IMessageProcessor messageProcessor) {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _expectedTag = expectedTag;
        }

        public SequencePosition Parse(ReadOnlySequence<byte> buffer) {
            long current = 0;
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
                        break;

                    case State.ReadTag:
                        if (_tagReader.Done(ReadNextByte())) {
                            var tag = _tagReader.Value;
                            if (tag != _expectedTag)
                                throw new InvalidDataException($"Invalid message tag {tag}");
                            _state = State.ReadLength;
                        }
                        break;

                    case State.ReadLength:
                        if (_lengthReader.Done(ReadNextByte())) {
                            _lengthToRead = _lengthReader.Value;
                            _state = _lengthToRead == 0 ? State.Init : State.ReadBytes;
                        }
                        break;

                    case State.ReadBytes:
                        _lengthToRead = ReadBytes(_lengthToRead);
                        if (_lengthToRead == 0) {
                            _messageProcessor.Process(_segments);
                            _state = State.Init;
                        }
                        break;
                    }
                }
            return buffer.End;
        }

        private readonly ulong _expectedTag;
        private readonly ILIntReader _lengthReader = new ILIntReader();
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
