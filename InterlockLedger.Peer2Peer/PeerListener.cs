/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal class PeerListener : IListener
    {
        public PeerListener(INodeSink nodeSink, ILogger<PeerListener> logger, IExternalAccessDiscoverer discoverer) {
            _nodeSink = nodeSink ?? throw new ArgumentNullException(nameof(nodeSink));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            Alive = true;
        }

        public bool Alive { get; private set; }

        public void Dispose() => Dispose(true);

        public async Task StartAsync() {
            _logger.LogInformation($"Starting listener for {_nodeSink.NetworkName} network on {_nodeSink.NetworkProtocolName} protocol!");
            (_address, _port, _listener) = await _discoverer.DetermineExternalAccessAsync(_nodeSink);
            _nodeSink.PublishedAs(_address, _port);
            _logger.LogInformation($"== Listening at {_address}:{_port}");
            new Thread(DoWork).Start();
        }

        public Task StopAsync() {
            if (!_abandon) {
                _abandon = true;
                _listener.Stop();
            }
            while (Alive)
                Task.Delay(_resolutionInMilliseconds);
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing && Alive) {
                    Task.WaitAll(StopAsync());
                }
                _disposedValue = true;
            }
        }

        private const int _resolutionInMilliseconds = 10;
        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly ILogger<PeerListener> _logger;
        private readonly INodeSink _nodeSink;
        private bool _abandon = false;
        private string _address;
        private bool _disposedValue = false;
        private TcpListener _listener;
        private int _port;

        // ? Use the new System.IO.Pipelines https://blogs.msdn.microsoft.com/dotnet/2018/07/09/system-io-pipelines-high-performance-io-in-net/ ?
        private async Task Background_Listen() {
            try {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                if (client != null && client.Connected) {
                    using (var stream = client.GetStream()) {
                        IPipeLine pipe = new PipeLine(stream);
                        try {
                            while (pipe.KeepGoing) {
                                await _nodeSink.SinkAsync(pipe);
                            }
                        } catch (Exception e) {
                            _logger.LogError(e, "-- Error while trying to process message from clients.");
                            await pipe.RejectAsync(-1);
                        }
                    }
                    client.Close();
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
    }
}