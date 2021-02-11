/******************************************************************************************************************************

Copyright (c) 2018-2021 InterlockLedger Network
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
using System.Buffers;
using System.Collections.Generic;

namespace InterlockLedger.Peer2Peer
{
    public readonly struct NetworkMessageSlice
    {
        public NetworkMessageSlice(ulong channel, params byte[] array) : this(channel, array, 0, array.Length) {
        }

        public NetworkMessageSlice(ulong channel, byte[] array, int start, int length) : this(channel, new ReadOnlyMemory<byte>(array, start, length)) {
        }

        public NetworkMessageSlice(ulong channel, params ReadOnlyMemory<byte>[] data) : this(channel, (IEnumerable<ReadOnlyMemory<byte>>)data) {
        }

        public NetworkMessageSlice(ulong channel, IEnumerable<ReadOnlyMemory<byte>> segmentList) {
            DataList = segmentList.ToSequence();
            Channel = channel;
        }

        public NetworkMessageSlice(ulong channel, ReadOnlySequence<byte> dataList) {
            DataList = dataList;
            Channel = channel;
        }

        public ulong Channel { get; }

        public ReadOnlySequence<byte> DataList { get; }

        public bool IsEmpty => DataList.IsEmpty;

        public NetworkMessageSlice Add(byte[] array) => Add(new ReadOnlyMemory<byte>(array));

        public NetworkMessageSlice Add(byte[] array, int start, int length) => Add(new ReadOnlyMemory<byte>(array, start, length));

        public NetworkMessageSlice Add(ReadOnlyMemory<byte> data) => new NetworkMessageSlice(Channel, DataList.Add(data));

        public NetworkMessageSlice WithChannel(ulong channel) => new NetworkMessageSlice(channel, DataList);
    }
}