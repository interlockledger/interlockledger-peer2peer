// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2022 InterlockLedger Network
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

using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    internal static class SocketExtensions
    {
        public static ValueTask<int> ReceiveAsync(this Socket socket, Memory<byte> memory, CancellationToken token)
            => ReceiveAsync(socket, memory, SocketFlags.None, token);

        public static ValueTask<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags, CancellationToken token)
            => SocketTaskExtensions.ReceiveAsync(socket, memory, socketFlags, token);

        public static ValueTask<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags, CancellationToken token)
            => SocketTaskExtensions.SendAsync(socket, buffer, socketFlags, token);

        public static Task<int> SendBuffersAsync(this Socket socket, IEnumerable<ReadOnlyMemory<byte>> buffers, CancellationToken token)
            => SendBuffersAsync(socket, buffers, SocketFlags.None, token);

        public static async Task<int> SendBuffersAsync(this Socket socket, IEnumerable<ReadOnlyMemory<byte>> buffers, SocketFlags socketFlags, CancellationToken token) {
            socket.Required(nameof(socket));
            var total = 0;
            foreach (var buffer in buffers.Required(nameof(buffers))) {
                var expected = buffer.Length;
                var sent = await SocketTaskExtensions.SendAsync(socket, buffer, socketFlags, token);
                total += sent;
                if (sent < expected)
                    break;
            }
            return total;
        }

        public static Task<int> SendBuffersAsync(this Socket socket, IEnumerable<ArraySegment<byte>> buffers, CancellationToken token)
            => SendBuffersAsync(socket, buffers, SocketFlags.None, token);
        public static Task<int> SendBuffersAsync(this Socket socket, IEnumerable<ArraySegment<byte>> buffers, SocketFlags socketFlags, CancellationToken token)
            => SendBuffersAsync(socket, buffers?.Cast<ReadOnlyMemory<byte>>(), socketFlags, token);

        public static Task<int> SendILintAsync(this Socket socket, ulong ilint)
            => socket.SendAsync(ilint.ILIntEncode(), SocketFlags.None);
    }
}