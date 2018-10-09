/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.IO;

namespace InterlockLedger.Common
{
    public static class ILIntHelpers
    {
        public const int ILINT_BASE = 0xF8;
        public const ulong ILINT_MAX = ulong.MaxValue - ILINT_BASE;

        public static ulong ILIntDecode(Func<byte> readByte) {
            ulong value = 0;
            var nextByte = readByte();
            if (nextByte < ILINT_BASE)
                return nextByte;
            var size = nextByte - ILINT_BASE + 1;
            while (size-- > 0)
                value = (value << 8) + readByte();
            if (value > ILINT_MAX)
                return 0;
            return value + ILINT_BASE;
        }

        public static byte[] ILIntEncode(this ulong value, byte[] buffer, int offset, int count) {
            ILIntEncode(value, b => {
                if (--count < 0)
                    throw new InvalidDataException("Buffer too small!!!");
                buffer[offset++] = b;
            });
            return buffer;
        }

        public static Stream ILIntEncode(this Stream stream, ulong value) {
            value.ILIntEncode(stream.WriteByte);
            return stream;
        }

        public static byte[] ILIntEncode(this ulong value) {
            var size = ILIntSize(value);
            return value.ILIntEncode(new byte[size], 0, size);
        }

        public static void ILIntEncode(this ulong value, Action<byte> writeByte) {
            if (writeByte == null) {
                throw new ArgumentNullException(nameof(writeByte));
            }

            var size = ILIntSize(value);
            if (size == 1) {
                writeByte((byte)(value & 0xFF));
            } else {
                writeByte((byte)(ILINT_BASE + (size - 2)));
                value = value - ILINT_BASE;
                for (var i = ((size - 2) * 8); i >= 0; i -= 8) {
                    writeByte((byte)((value >> i) & 0xFF));
                }
            }
        }

        public static int ILIntSize(this ulong value) {
            if (value < ILINT_BASE)
                return 1;
            if (value <= (0xFF + ILINT_BASE))
                return 2;
            if (value <= (0xFFFF + ILINT_BASE))
                return 3;
            if (value <= (0xFFFFFF + ILINT_BASE))
                return 4;
            if (value <= (0xFFFFFFFFL + ILINT_BASE))
                return 5;
            if (value <= (0xFF_FFFF_FFFF + ILINT_BASE))
                return 6;
            if (value <= (0xFFFF_FFFF_FFFFL + ILINT_BASE))
                return 7;
            if (value <= ((ulong)0xFF_FFFF_FFFF_FFFFL + ILINT_BASE))
                return 8;
            return 9;
        }
    }
}