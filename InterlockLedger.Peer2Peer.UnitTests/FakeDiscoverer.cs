// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2023 InterlockLedger Network
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met
//
// * Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// * Neither the name of the copyright holder nor the names of its
//   contributors may be used to endorse or promote products derived from
//   this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES, LOSS OF USE, DATA, OR PROFITS, OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// ******************************************************************************************************************************

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal class FakeDiscoverer : IExternalAccessDiscoverer
    {
        public FakeDiscoverer() => PortDelta = 1;

        public ushort PortDelta { get; }

        public Task<ExternalAccess> DetermineExternalAccessAsync(INodeSink nodeSink)
            => DetermineExternalAccessAsync(nodeSink.Required(nameof(nodeSink)).HostAtAddress,
                                            nodeSink.HostAtPortNumber,
                                            nodeSink.PublishAtAddress,
                                            nodeSink.PublishAtPortNumber);

        public Task<ExternalAccess> DetermineExternalAccessAsync(string hostAtAddress, ushort hostAtPortNumber, string publishAtAddress, ushort? publishAtPortNumber) {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, hostAtPortNumber));
            listenSocket.Listen(10);
            return Task.FromResult(new ExternalAccess(listenSocket, hostAtAddress, hostAtPortNumber, publishAtAddress, publishAtPortNumber));
        }

        public void Dispose() {
            // Method intentionally left empty.
        }
    }
}