/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;

namespace InterlockLedger.Common
{
    public class ILIntReader
    {
        public ILIntReader() => Reset();

        public ulong Value => _size == 0 ? _value : throw new InvalidOperationException("ILInt still not completely read");

        public bool Done(byte nextByte) {
            if (_size < 0) {
                _value = nextByte;
                if (nextByte < ILIntHelpers.ILINT_BASE) {
                    _size = 0;
                    return true;
                }
                _size = nextByte - ILIntHelpers.ILINT_BASE + 1;
                return false;
            }
            _value = (_value << 8) + nextByte;
            if (_value > ILIntHelpers.ILINT_MAX)
                throw new InvalidOperationException("Decoded ILInt value is too large");
            if (_size-- > 0)
                return false;
            _value += ILIntHelpers.ILINT_BASE;
            return true;
        }

        public void Reset() {
            _size = -1;
            _value = 0;
        }

        private int _size;
        private ulong _value;
    }
}