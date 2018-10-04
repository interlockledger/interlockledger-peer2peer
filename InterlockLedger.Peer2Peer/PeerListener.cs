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
            (_address, _port, _listener) = await _discoverer.DetermineExternalAccessAsync(_nodeSink);
            _nodeSink.PublishedAs(_address, _port);
            _logger.LogInformation($"-- Started listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network at {_address}:{_port}!");
            new Thread(DoWork).Start();
        }
        /*
         *  Mirrors
        2018-10-04 16:28:40 [NodeListener] Information: -- Mirror of Chain 'Genesis InterlockRecord for Network 9C.7D.11.F0' from 'genesis' #CKUiSY_iS0OaZcss8-SDm1sa2uEx_xgh49xh3A6bnKk [ACTIVE]
        2018-10-04 16:28:40 [PeerListener] Information: Starting listener for Vesta network on ilkl protocol!
        2018-10-04 16:28:40 [PeerListener] Information: == Listening at localhost:32017
         * */
        public void Stop() => Task.WaitAll(StopAsync());

        public Task StopAsync() {
            if (!_abandon) {
                _logger.LogInformation("PeerListener");
                _logger.LogInformation($"-- Stopped listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network!!!");
                _abandon = true;
                try {
                    _listener.Stop();
                } catch (ObjectDisposedException e) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                }
            }
            while (Alive)
                Task.Delay(_resolutionInMilliseconds);
            return Task.CompletedTask;
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