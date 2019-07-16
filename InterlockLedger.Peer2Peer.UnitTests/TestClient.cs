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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class TestClient : IConnection, IActiveChannel
    {
        public Pipeline Pipeline;

        public TestClient(string id) => Id = id ?? throw new ArgumentNullException(nameof(id));

        public bool Active => true;
        public ulong Channel { get; private set; }
        public IConnection Connection => this;
        public string Id { get; }
        public int ListeningBufferSize => 1024;
        public ulong MessageTag { get; }
        public string NetworkName { get; }
        public string NetworkProtocolName { get; }
        public bool Connected => Pipeline?.Connected ?? false;

        public IActiveChannel AllocateChannel(IChannelSink channelSink) => this;

        public void Dispose() => Stop();

        public IActiveChannel GetChannel(ulong channel) {
            Channel = channel;
            return this;
        }

        public bool Send(IEnumerable<byte> message) {
            if (message != null)
                Pipeline?.Send(new NetworkMessageSlice(Channel, message));
            return true;
        }

        public void SetDefaultSink(IChannelSink sink) => throw new NotImplementedException();

        public Task<Success> SinkAsync(IEnumerable<byte> message) => throw new NotImplementedException();

        public void Stop() => Pipeline?.Stop();
    }
}