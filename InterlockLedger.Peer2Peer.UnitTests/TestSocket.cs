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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class TestSocket : ISocket
    {
        public readonly Memory<byte> _bytesReceived;
        public readonly CancellationTokenSource _source;
        public readonly List<ArraySegment<byte>> bytesSent = new List<ArraySegment<byte>>();
        public int _receivedCount;

        public TestSocket(CancellationTokenSource source, params byte[] bytesReceived) {
            _source = source;
            _bytesReceived = bytesReceived ?? throw new ArgumentNullException(nameof(bytesReceived));
            _holdYourHorses = false;
        }
        public TestSocket(CancellationTokenSource source, bool holdYourHorses, params byte[] bytesReceived) {
            _source = source;
            _bytesReceived = bytesReceived ?? throw new ArgumentNullException(nameof(bytesReceived));
            _holdYourHorses = holdYourHorses;
        }

        public int Available => _bytesReceived.Length - _receivedCount;
        public IList<ArraySegment<byte>> BytesSent => bytesSent;
        public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 13013);

        public void Dispose() {
            // Do nothing
        }

        public async Task<int> ReceiveAsync(Memory<byte> memory, SocketFlags socketFlags, CancellationToken token) {
            while (_holdYourHorses)
                await Task.Yield();
            if (_bytesReceived.Length > _receivedCount) {
                int howMany = Math.Min(memory.Length, _bytesReceived.Length - _receivedCount);
                Memory<byte> slice = _bytesReceived.Slice(_receivedCount, howMany);
                if (slice.TryCopyTo(memory)) {
                    _receivedCount += slice.Length;
                    return slice.Length;
                }
            }
            return 0;
        }

        public void ReleaseYourHorses() => _holdYourHorses = false;

        public Task SendAsync(IList<ArraySegment<byte>> segment) {
            bytesSent.AddRange(segment);
            if (Available == 0)
                _source.CancelAfter(100);
            return Task.CompletedTask;
        }

        public void Shutdown(SocketShutdown how) {
            // Do nothing
        }

        public void Stop() => throw new NotImplementedException();

        private bool _holdYourHorses;
    }
}