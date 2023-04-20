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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    public class NetSocket : AbstractDisposable, ISocket
    {
        public NetSocket(Socket socket) {
            _socket = socket.Required(nameof(socket));
            RemoteEndPoint = _socket.RemoteEndPoint;
        }

        public int Available => Do(() => _socket.Available);
        public bool Connected => Do(() => _socket.Connected);
        public EndPoint RemoteEndPoint { get; }

        public Task<int> ReceiveAsync(Memory<byte> memory, SocketFlags socketFlags, CancellationToken token)
            => DoAsync(async ()
                => token.IsCancellationRequested
                    ? 0
                    : await _socket.ReceiveAsync(memory, socketFlags, token).ConfigureAwait(false));

        public Task<long> SendBuffersAsync(ReadOnlySequence<byte> buffers, SocketFlags socketFlags, CancellationToken token)
            => DoAsync(async () => {
                try {
                    long count = 0;
                    var pos = buffers.Start;
                    while (buffers.TryGet(ref pos, out var mem))
                        count += await _socket.SendAsync(mem, socketFlags, token).ConfigureAwait(false);
                    return count;
                } catch (SocketException) {
                    return -1;
                }
            });

        protected override void DisposeManagedResources() {
            try {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Dispose();
            } catch (ObjectDisposedException e) {
                Debug.WriteLine($"Ignored: {e}");
            } catch (SocketException e) {
                Debug.WriteLine($"This should not happen: {e}");
            }
        }

        private readonly Socket _socket;
    }
}