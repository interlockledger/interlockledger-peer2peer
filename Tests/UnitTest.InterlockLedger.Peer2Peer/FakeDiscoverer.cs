/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Peer2Peer;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace UnitTest.InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal class FakeDiscoverer : IExternalAccessDiscoverer
    {
        public FakeDiscoverer() {
        }

        public Task<ExternalAccess> DetermineExternalAccessAsync(INodeSink nodeSink) {
            if (nodeSink == null)
                throw new ArgumentNullException(nameof(nodeSink));
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 32015));
            return Task.FromResult(new ExternalAccess(listenSocket, nodeSink.HostAtAddress, nodeSink.HostAtPortNumber, nodeSink.PublishAtAddress, nodeSink.PublishAtPortNumber));
        }

        public void Dispose() {
            // Method intentionally left empty.
        }
    }
}