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
    // TODO: to be replaced by some implementation that deals with NAT/UPnP/Whatever to give a public address and port
    public class DummyExternalAccessDiscoverer : IExternalAccessDiscoverer
    {
        public DummyExternalAccessDiscoverer(ILoggerFactory loggerFactory) => _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<DummyExternalAccessDiscoverer>();

        public Task<(string, int, TcpListener)> DetermineExternalAccessAsync(INodeSink nodeSink) {
            string defaultAddress = nodeSink.DefaultAddress ?? "localhost";
            IPAddress localaddr = GetAddress(defaultAddress);
            var listener = GetListener(localaddr, (ushort)nodeSink.DefaultPort);
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return Task.FromResult((defaultAddress, port, listener));
        }

        private readonly ILogger<DummyExternalAccessDiscoverer> _logger;

        private static IPAddress GetAddress(string name) {
            if (IPAddress.TryParse(name, out IPAddress address))
                return address;
            IPAddress[] addressList = Dns.GetHostEntry(name).AddressList;
            return addressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? addressList.First();
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
    }
}