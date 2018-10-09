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
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal static class Extensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags) {
            var arraySegment = GetArray(memory);
            return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
        }

        private static ArraySegment<byte> GetArray(Memory<byte> memory) => GetArray((ReadOnlyMemory<byte>)memory);

        private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory) {
            if (!MemoryMarshal.TryGetArray(memory, out var result)) {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }
    }

    internal class PeerListener : IListener
    {
        public PeerListener(INodeSink nodeSink, ILogger logger, IExternalAccessDiscoverer discoverer) {
            _nodeSink = nodeSink ?? throw new ArgumentNullException(nameof(nodeSink));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            Alive = true;
        }

        public bool Alive { get; private set; }

        public void Dispose() => Dispose(true);

        public void Start() => Task.WaitAll(StartAsync());

        public async Task StartAsync() {
            _logger.LogInformation("PeerListener");
            (_address, _port, _listenSocket) = await _discoverer.DetermineExternalAccessAsync(_nodeSink);
            _nodeSink.PublishedAs(_address, _port);
            _logger.LogInformation($"-- Started listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network at {_address}:{_port}!");
            new Thread(DoWork).Start();
        }

        public void Stop() => Task.WaitAll(StopAsync());

        public Task StopAsync() {
            if (!_abandon) {
                _logger.LogInformation("PeerListener");
                _logger.LogInformation($"-- Stopped listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network!!!");
                _abandon = true;
                try {
                    _listenSocket.Close(10);
                } catch (ObjectDisposedException e) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                }
            }
            while (Alive)
                Task.Delay(_resolutionInMilliseconds);
            return Task.CompletedTask;
        }

        internal static async Task ProcessLinesAsync(Socket socket, INodeSink nodeSink) {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");
            var pipe = new Pipe();
            Task writing = FillPipeAsync(socket, pipe.Writer);
            Task reading = ReadPipeAsync(socket, pipe.Reader, nodeSink);
            await Task.WhenAll(reading, writing);
            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing && Alive) {
                    Stop();
                }
                _disposedValue = true;
            }
        }

        private const int _resolutionInMilliseconds = 10;
        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly ILogger _logger;
        private readonly INodeSink _nodeSink;
        private bool _abandon = false;
        private string _address;
        private bool _disposedValue = false;
        private Socket _listenSocket;
        private int _port;

        private static async Task FillPipeAsync(Socket socket, PipeWriter writer) {
            const int minimumBufferSize = 512;
            while (true) {
                try {
                    // Request a minimum of 512 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0) {
                        break;
                    }
                    // Tell the PipeWriter how much was read
                    writer.Advance(bytesRead);
                } catch {
                    break;
                }
                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();
                if (result.IsCompleted) {
                    break;
                }
            }
            // Signal to the reader that we're done writing
            writer.Complete();
        }

        private static async Task ReadPipeAsync(Socket socket, PipeReader reader, INodeSink nodeSink) {
            var parser = new MessageParser(nodeSink.MessageTag, reader);
            while (true) {
                ReadResult result = await reader.ReadAsync();
                if (result.IsCanceled)
                    break;
                try {
                    parser.ParseMessage(result.Buffer, socket, nodeSink.SinkAsync);
                } catch (Exception e) {
                    reader.Complete(e);
                }
                if (result.IsCompleted)
                    break;
            }
            reader.Complete();
        }

        private async Task Background_Listen() {
            try {
                while (true) {
                    var socket = await _listenSocket.AcceptAsync();
                    await ProcessLinesAsync(socket, _nodeSink);
                }
            } catch (AggregateException e) when (e.InnerExceptions.Any(ex => ex is ObjectDisposedException)) {
                _logger.LogTrace(e, "ObjectDisposedException");
            } catch (ObjectDisposedException e) {
                _logger.LogTrace(e, "ObjectDisposedException");
            } catch (Exception e) {
                _logger.LogError(e, $"-- Error while trying to listen to clients.");
            }
        }

        // TODO2: Implement something more like Kestrel does for scaling up multiple simultaneous requests processing
        private void DoWork() {
            do {
                try {
                    Task.WaitAll(Background_Listen());
                } catch (Exception e) {
                    _logger.LogError(e, "Error while listening");
                }
            } while (SleepAtMost(30));
            Dispose();
            Alive = false;
        }

        private bool SleepAtMost(int millisecondsTimeout) {
            while (millisecondsTimeout > 0 && !_abandon) {
                Thread.Sleep(_resolutionInMilliseconds);
                millisecondsTimeout -= _resolutionInMilliseconds;
            }
            return !_abandon;
        }

        private class MessageParser
        {
            public MessageParser(ulong expectedTag, PipeReader reader) {
                _reader = reader ?? throw new ArgumentNullException(nameof(reader));
                _expectedTag = expectedTag;
            }

            public void ParseMessage(ReadOnlySequence<byte> buffer, Socket socket, Func<Socket, IEnumerable<ReadOnlyMemory<byte>>, Task> process) {
                if (buffer.IsEmpty)
                    return;
                long current = 0;
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
                        if (_tagReader.Done(ReadNextByte(ref buffer, ref current))) {
                            var tag = _tagReader.Value;
                            if (tag != _expectedTag)
                                throw new InvalidDataException($"Invalid message tag {tag}");
                            _state = State.ReadLength;
                        }
                        break;

                    case State.ReadLength:
                        if (_lengthReader.Done(ReadNextByte(ref buffer, ref current))) {
                            _lengthToRead = _lengthReader.Value;
                            _state = _lengthToRead == 0 ? State.Init : State.ReadBytes;
                        }
                        break;

                    case State.ReadBytes:
                        _lengthToRead = ReadBytes(buffer, ref current, _lengthToRead);
                        if (_lengthToRead == 0) {
                            Task.WaitAll(process(socket, _segments));
                            _state = State.Init;
                        }
                        break;
                    }
                }
            }

            private readonly ulong _expectedTag;
            private readonly ILIntReader _lengthReader = new ILIntReader();
            private readonly PipeReader _reader;
            private readonly List<ReadOnlyMemory<byte>> _segments = new List<ReadOnlyMemory<byte>>();
            private readonly ILIntReader _tagReader = new ILIntReader();
            private ulong _lengthToRead;
            private State _state = State.Init;

            private ulong ReadBytes(ReadOnlySequence<byte> buffer, ref long current, ulong length) {
                if (length >= int.MaxValue)
                    throw new InvalidOperationException("Too many bytes to read!");
                long bytesToRead = Math.Min((long)length, buffer.Length - current);
                var slice = buffer.Slice(current, bytesToRead);
                foreach (var segment in slice)
                    _segments.Add(segment);
                current += bytesToRead;
                _reader.AdvanceTo(slice.End);
                return length - (ulong)bytesToRead;
            }

            private byte ReadNextByte(ref ReadOnlySequence<byte> buffer, ref long current) {
                var b = (new byte[1]).AsSpan();
                var slice = buffer.Slice(current++, 1);
                slice.CopyTo(b);
                _reader.AdvanceTo(slice.End);
                return b[0];
            }

            private enum State
            {
                Init,
                ReadTag,
                ReadLength,
                ReadBytes
            }
        }
    }
}