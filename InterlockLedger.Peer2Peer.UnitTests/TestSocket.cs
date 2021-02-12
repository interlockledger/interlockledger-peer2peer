// ******************************************************************************************************************************
//
// Copyright (c) 2018-2021 InterlockLedger Network
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
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class TestSocket : AbstractDisposable, ISocket
    {
        public TestSocket(params byte[] bytesReceived) : this(false, bytesReceived) {
        }

        public TestSocket(bool holdYourHorses, params byte[] bytesReceived) {
            _bytesReceived = bytesReceived.Required(nameof(bytesReceived));
            _holdYourHorses = holdYourHorses;
        }

        public int Available => _holdYourHorses ? 0 : _bytesReceived.Length - _receivedCount;
        public ReadOnlySequence<byte> BytesSent { get; private set; } = ReadOnlySequence<byte>.Empty;
        public bool Connected => _holdYourHorses || Available > 0;
        public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 13013);

        public async Task<int> ReceiveAsync(Memory<byte> memory, SocketFlags socketFlags, CancellationToken token) {
            while (_holdYourHorses)
                await Task.Yield();
            if (_bytesReceived.Length > _receivedCount) {
                int howMany = Math.Min(memory.Length, _bytesReceived.Length - _receivedCount);
                var slice = _bytesReceived.Slice(_receivedCount, howMany);
                if (slice.TryCopyTo(memory)) {
                    _receivedCount += slice.Length;
                    return slice.Length;
                }
            }
            return 0;
        }

        public void ReleaseYourHorses() => _holdYourHorses = false;

        public Task<long> SendBuffersAsync(ReadOnlySequence<byte> buffers, SocketFlags socketFlags, CancellationToken token) {
            BytesSent = BytesSent.Add(buffers).Realloc();
            return Task.FromResult(buffers.Length);
        }

        protected override void DisposeManagedResources() {
        }

        private readonly ReadOnlyMemory<byte> _bytesReceived;
        private bool _holdYourHorses;
        private int _receivedCount;
    }
}