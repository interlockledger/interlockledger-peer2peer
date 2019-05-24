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
using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal sealed class ListenerClient : BasePeerClient
    {
        public ListenerClient(string id, ulong tag, Socket socket, BaseListener origin, int defaultListeningBufferSize)
            : base(id, tag, origin._source, origin._logger, defaultListeningBufferSize) {
            if (socket is null)
                throw new ArgumentNullException(nameof(socket));
            var ipEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            NetworkAddress = ipEndPoint.Address.ToString();
            NetworkPort = ipEndPoint.Port;
            _socket = socket;
            _origin = origin;
            StartPipeline();
        }

        public override void PipelineStopped() => _origin.PipelineStopped();

        protected internal override Task<Success> SinkAsync(NetworkMessageSlice slice, IResponder responder) => _origin.SinkAsync(slice, responder);

        protected override NetworkMessageSlice AdjustSlice(NetworkMessageSlice slice, ISink clientSink) {
            if (clientSink != null)
                throw new InvalidOperationException("Listener Client can't handle this????");
            return slice;
        }

        protected override Socket BuildSocket() => _socket;

        private readonly BaseListener _origin;
        private readonly Socket _socket;
    }
}
