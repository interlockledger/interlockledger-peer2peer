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
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    // TODO1: to be replaced by some implementation that deals with NAT/UPnP/Whatever to really give the node a public address and port
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    public class DummyExternalAccessDiscoverer : IExternalAccessDiscoverer
    {
        public DummyExternalAccessDiscoverer(ILoggerFactory loggerFactory)
            => _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<DummyExternalAccessDiscoverer>();

        public Task<ExternalAccess> DetermineExternalAccessAsync(INodeSink nodeSink) {
            if (_disposedValue)
                return Task.FromResult<ExternalAccess>(null);
            string hostingAddress = nodeSink.HostAtAddress ?? "localhost";
            IPAddress localaddr = GetAddress(hostingAddress);
            var listener = GetSocket(localaddr, (ushort)nodeSink.HostAtPortNumber);
            var port = ((IPEndPoint)listener.LocalEndPoint).Port;
            return Task.FromResult(new ExternalAccess (hostingAddress, port, hostingAddress, port, listener));
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                // really nothing to do in this dumb implementation
                if (disposing) {
                    // dispose managed state
                }
                _disposedValue = true;
            }
        }

        private readonly ILogger<DummyExternalAccessDiscoverer> _logger;
        private bool _disposedValue = false;

        private static IPAddress GetAddress(string name) {
            if (IPAddress.TryParse(name, out IPAddress address))
                return address;
            IPAddress[] addressList = Dns.GetHostEntry(name).AddressList;
            return addressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? addressList.First();
        }

        private Socket GetSocket(IPAddress localaddr, ushort portNumber) {
            Socket InnerGetSocket(ushort port) {
                try {
                    var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    listenSocket.Bind(new IPEndPoint(localaddr, port));
                    listenSocket.Listen(120);
                    return listenSocket;
                } catch (Exception e) {
                    _logger.LogError(e, $"-- Error while trying to listen to clients.");
                    return null;
                }
            }

            for (ushort tries = 5; tries > 0; tries--) {
                var socket = InnerGetSocket(portNumber);
                if (socket != null)
                    return socket;
                portNumber -= 18;
            }
            return InnerGetSocket(0);
        }
    }
}