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

using System;
using System.Net;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    // TODO1: to be replaced by some implementation that deals with NAT/UPnP/Whatever to really give the node a public address and port
    public class DummyExternalAccessDiscoverer : AbstractExternalAccessDiscoverer
    {
        public DummyExternalAccessDiscoverer(SocketFactory socketFactory)
            => _socketFactory = socketFactory ?? throw new ArgumentNullException(nameof(socketFactory));

        public override Task<ExternalAccess> DetermineExternalAccessAsync(string hostAtAddress, ushort hostAtPortNumber, string publishAtAddress, ushort? publishAtPortNumber) {
            if (Disposed)
                return Task.FromResult<ExternalAccess>(null);
            string hostingAddress = hostAtAddress ?? "localhost";
            var listener = _socketFactory.GetSocket(hostingAddress, hostAtPortNumber);
            if (listener is null)
                throw new InterlockLedgerIOException($"Could not open a listening socket for {hostingAddress}:{hostAtPortNumber}");
            var port = (ushort)((IPEndPoint)listener.LocalEndPoint).Port;
            return Task.FromResult(new ExternalAccess(listener, hostingAddress, port, publishAtAddress, publishAtPortNumber));
        }

        protected override void DisposeManagedResources() => _socketFactory.Dispose();

        private readonly SocketFactory _socketFactory;
    }
}