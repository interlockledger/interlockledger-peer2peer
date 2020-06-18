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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal class FakeNodeSink : AbstractDisposable, INodeSink
    {
        public readonly List<IEnumerable<byte>> MessagesReceived = new List<IEnumerable<byte>>();

        public FakeNodeSink(ulong messageTag, ushort port, int inactivityTimeoutInMinutes, int maxConcurrentConnections, params byte[] response) {
            MessageTag = messageTag;
            HostAtPortNumber = port;
            _response = response;
        }

        public ulong Channel { get; set; } = 0;
        public string HostAtAddress => "localhost";
        public ushort HostAtPortNumber { get; }
        public string Id => NodeId;
        public int InactivityTimeoutInMinutes { get; }
        public int ListeningBufferSize => 1024;
        public IEnumerable<string> LocalResources { get; } = new string[] { "DummyDoc1", "DummyDoc2" };
        public int MaxConcurrentConnections { get; }
        public ulong MessageTag { get; }
        public string NetworkName => "UnitTesting";
        public string NetworkProtocolName => "UnitTest";
        public string NodeId => "DummyNode";
        public string PublishAtAddress => HostAtAddress;
        public ushort? PublishAtPortNumber => HostAtPortNumber;
        public IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "None" };

        public void HostedAt(string address, ushort port) {
            // Do nothing
        }

        public void PublishedAt(string address, ushort port) {
            // Do nothing
        }

        public virtual async Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel) {
            MessagesReceived.Add(message);
            if (_response.Length > 0)
                await channel.SendAsync(_response);
            return Success.Exit;
        }

        protected override void DisposeManagedResources() {
        }

        private readonly byte[] _response;
    }
}
