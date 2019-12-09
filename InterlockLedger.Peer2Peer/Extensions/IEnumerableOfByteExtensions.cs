using System;
using System.Collections.Generic;
using System.Linq;

namespace InterlockLedger.Peer2Peer
{
    public static class IEnumerableOfByteExtensions
    {
        public static string ToUrlSafeBase64(this IEnumerable<byte> bytes)
            => ToUrlSafeBase64(bytes.ToArray());

        public static string ToUrlSafeBase64(this byte[] bytes)
            => Convert.ToBase64String(bytes ?? throw new ArgumentNullException(nameof(bytes))).Trim('=').Replace('+', '-').Replace('/', '_');

        public static string ToUrlSafeBase64(this ReadOnlyMemory<byte> readOnlyBytes)
            => ToUrlSafeBase64(readOnlyBytes.ToArray());
    }
}