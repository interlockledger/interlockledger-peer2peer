/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal class PeerListener : IListener
    {
        public PeerListener(INodeSink nodeSink, ILogger logger, IExternalAccessDiscoverer discoverer, CancellationTokenSource source) {
            _nodeSink = nodeSink ?? throw new ArgumentNullException(nameof(nodeSink));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _token = source.Token;
            _token.Register(Stop);
            _minimumBufferSize = Math.Max(512, _nodeSink.DefaultListeningBufferSize);
        }

        public bool Alive => _listenSocket != null;

        public void Dispose() => Stop();

        public void Start() => Task.WaitAll(StartAsync());

        public void Stop() {
            if (!_source.IsCancellationRequested)
                _source.Cancel();
            if (Alive) {
                _logger.LogInformation($"-- Stopped listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network!!!");
                try {
                    _listenSocket.Close(10);
                } catch (ObjectDisposedException e) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                }
                _listenSocket = null;
            }
        }

        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly ILogger _logger;
        private readonly int _minimumBufferSize;
        private readonly INodeSink _nodeSink;
        private readonly CancellationTokenSource _source;
        private readonly CancellationToken _token;

        private string _address;
        private Socket _listenSocket;
        private int _port;

        // TODO2: Implement something more like Kestrel does for scaling up multiple simultaneous requests processing
        private async Task Listen() {
            _logger.LogInformation($"-- Started listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network at {_address}:{_port}!");
            do {
                try {
                    while (!_token.IsCancellationRequested) {
                        var socket = await _listenSocket.AcceptAsync();
                        _logger.LogTrace($"[{socket.RemoteEndPoint}]: connected");
                        var pipe = new Pipe();
                        Task writing = PipeFillAsync(socket, pipe.Writer);
                        Task reading = PipeReadAsync(socket, pipe.Reader, _nodeSink);
                        await Task.WhenAll(reading, writing);
                        _logger.LogTrace($"[{socket.RemoteEndPoint}]: disconnected");
                    }
                } catch (AggregateException e) when (e.InnerExceptions.Any(ex => ex is ObjectDisposedException)) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                } catch (ObjectDisposedException e) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                } catch (SocketException e) {
                    _logger.LogTrace(e, $"-- Socket was killed");
                    break;
                } catch (Exception e) {
                    _logger.LogError(e, $"-- Error while trying to listen to clients.");
                }
            } while (!_token.IsCancellationRequested);
        }

        private async Task PipeFillAsync(Socket socket, PipeWriter writer) {
            while (!_token.IsCancellationRequested) {
                try {
                    // Request a minimum of 4096 bytes from the PipeWriter
                    _logger.LogTrace($"Getting {_minimumBufferSize} bytes to receive in the socket");
                    Memory<byte> memory = writer.GetMemory(_minimumBufferSize);
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

        private async Task PipeReadAsync(Socket socket, PipeReader reader, INodeSink messageProcessor) {
            var responder = new SocketResponder(socket);
            var parser = new MessageParser(messageProcessor.MessageTag, (bytes) => messageProcessor.SinkAsNodeAsync(bytes, responder.Respond).Result, _logger);
            while (!_token.IsCancellationRequested) {
                ReadResult result = await reader.ReadAsync(_token);
                if (result.IsCanceled)
                    break;
                try {
                    reader.AdvanceTo(parser.Parse(result.Buffer));
                } catch (Exception e) {
                    _logger.LogError(e, "While parsing message");
                    reader.Complete(e);
                    return;
                }
                if (result.IsCompleted)
                    break;
            }
            reader.Complete();
        }

        private async Task StartAsync() {
            if (_source.IsCancellationRequested)
                return;
            (_address, _port, _listenSocket) = await _discoverer.DetermineExternalAccessAsync(_nodeSink);
            _nodeSink.PublishedAs(_address, _port);
            new Thread(async () => await Listen()).Start();
        }
    }
}