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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class ListenerForPeer : ListenerCommon
    {
        public ListenerForPeer(INodeSink nodeSink, IExternalAccessDiscoverer discoverer, CancellationTokenSource source, ILogger logger)
            : base(nodeSink.NodeId, nodeSink, source, logger)
            => (_nodeSink, _socket, _route) = DetermineExternalAccess(
                    nodeSink ?? throw new ArgumentNullException(nameof(nodeSink)),
                    discoverer ?? throw new ArgumentNullException(nameof(discoverer)));

        public override Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel)
            => DoAsync(() => _nodeSink.SinkAsync(message, channel), Success.Exit);

        protected override string HeaderText
            => $"listening {NetworkProtocolName} protocol in {NetworkName} network at {_route}!";

        protected override string IdPrefix => "Listener";

        protected override Socket BuildSocket() => _socket;

        private readonly INodeSink _nodeSink;
        private readonly string _route;
        private readonly Socket _socket;

        private (INodeSink, Socket, string) DetermineExternalAccess(INodeSink nodeSink, IExternalAccessDiscoverer discoverer) {
            var externalAccess = discoverer.DetermineExternalAccessAsync(nodeSink).Result;
            nodeSink.HostedAt(externalAccess.InternalAddress, externalAccess.InternalPort);
            nodeSink.PublishedAt(externalAccess.ExternalAddress, externalAccess.ExternalPort);
            ExternalAddress = externalAccess.ExternalAddress;
            ExternalPortNumber = externalAccess.ExternalPort;
            return (nodeSink, externalAccess.Socket, externalAccess.Route);
        }
    }
}