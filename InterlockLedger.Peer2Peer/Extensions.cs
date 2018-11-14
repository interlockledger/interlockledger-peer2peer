/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal static class Extensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory)
            => SocketTaskExtensions.ReceiveAsync(socket, GetArray(memory), SocketFlags.None);

        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
            => SocketTaskExtensions.ReceiveAsync(socket, GetArray(memory), socketFlags);

        public static Task<int> SendAsync(this Socket socket, ArraySegment<byte> buffer)
            => SocketTaskExtensions.SendAsync(socket, buffer, SocketFlags.None);

        public static Task<int> SendAsync(this Socket socket, IList<ArraySegment<byte>> buffers)
            => SocketTaskExtensions.SendAsync(socket, buffers, SocketFlags.None);

        public static string ToBase64(this ReadOnlyMemory<byte> bytes) => Convert.ToBase64String(bytes.ToArray());

        private static ArraySegment<byte> GetArray(Memory<byte> memory) => GetArray((ReadOnlyMemory<byte>)memory);

        private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory) {
            if (!MemoryMarshal.TryGetArray(memory, out var result)) {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }
    }
}