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

namespace InterlockLedger.Common
{
    public class ILIntReader
    {
        public ILIntReader() => Reset();

        public ulong Value => _size == 0 ? _value : throw new InvalidOperationException("ILInt still not completely read");

        public bool Done(byte nextByte) {
            if (_size < 0) {
                if (nextByte < ILIntHelpers.ILINT_BASE) {
                    _value = nextByte;
                    _size = 0;
                    return true;
                }
                _size = nextByte - ILIntHelpers.ILINT_BASE + 1;
                return false;
            }
            _value = (_value << 8) + nextByte;
            if (_value > ILIntHelpers.ILINT_MAX)
                throw new InvalidOperationException("Decoded ILInt value is too large");
            if (--_size > 0)
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
