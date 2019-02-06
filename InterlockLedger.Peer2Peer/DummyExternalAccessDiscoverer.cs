/******************************************************************************************************************************
 
Copyright (c) 2018-2019 InterlockLedger Network
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of the copyright holder nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

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
            var listener = GetSocket(localaddr, nodeSink.HostAtPortNumber);
            var port = (ushort)((IPEndPoint)listener.LocalEndPoint).Port;
            return Task.FromResult(new ExternalAccess(listener, hostingAddress, port, nodeSink.PublishAtAddress, nodeSink.PublishAtPortNumber));
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