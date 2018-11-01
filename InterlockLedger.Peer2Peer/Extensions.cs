/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal static class Extensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags) {
            var arraySegment = GetArray(memory);
            return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
        }

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