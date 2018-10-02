/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal class PeerListener : IListener
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    {
        public static async Task<IListener> Create(INodeSink nodeSink, ILogger<PeerServices> logger) {
            var listener = new PeerListener(nodeSink, logger);
            await listener.StartAsync();
            return listener;
        }

        public void Dispose() => Dispose(true);

        public Task StopAsync() => throw new NotImplementedException();

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        private readonly ILogger<PeerServices> _logger;

        private readonly INodeSink _nodeSink;

        private string _address;

        private bool _disposedValue = false;

        private TcpListener _listener;

        private int _port;
        private Thread _thread;

        private PeerListener(INodeSink nodeSink, ILogger<PeerServices> logger) {
            _nodeSink = nodeSink;
            _logger = logger;
        }

        private static IPAddress GetAddress(string name) {
            if (IPAddress.TryParse(name, out IPAddress address))
                return address;
            IPAddress[] addressList = Dns.GetHostEntry(name).AddressList;
            return addressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? addressList.First();
        }

        private async Task Background_Listen() {
            try {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                if (client?.Connected ?? false) {
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

        private Task<(string, int)> DetermineExternalAccessAsync(string defaultAddress, int defaultPort) {
            IPAddress localaddr = GetAddress(defaultAddress);
            _listener = GetListener(localaddr, (ushort)defaultPort);
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            return Task.FromResult((defaultAddress, port));
        }

        private TcpListener GetListener(IPAddress localaddr, ushort portNumber) {
            TcpListener InnerGetListener(ushort port) {
                try {
                    var listener = new TcpListener(localaddr, port);
                    listener.Start(1024);
                    return listener;
                } catch (Exception e) {
                    _logger.LogError(e, $"-- Error while trying to listen to clients.");
                    return null;
                }
            }

            for (ushort tries = 5; tries > 0; tries--) {
                var tcpListener = InnerGetListener(portNumber);
                if (tcpListener != null)
                    return tcpListener;
                portNumber -= 18;
            }
            return InnerGetListener(0);
        }

        private async Task StartAsync() {
            (_address, _port) = await DetermineExternalAccessAsync("localhost", _nodeSink.DefaultPort);
            _nodeSink.PublishedAs(_address, _port);
            _logger.LogInformation($"Listening protocol {_nodeSink.NetworkProtocolName} at {_address}:{_port}");
            _thread = new Thread(() => Task.WaitAll(Background_Listen()));
            _thread.Start();
        }
    }
}