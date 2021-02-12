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
using System.Linq;
using System.Runtime.InteropServices;

namespace InterlockLedger.Peer2Peer
{
    public static class ReadOnlyMemoryExtensions
    {
        public static ArraySegment<byte> ToArraySegment(this ReadOnlyMemory<byte> memory)
            => !MemoryMarshal.TryGetArray(memory, out var result)
                ? throw new InvalidOperationException("Buffer backed by array was expected")
                : result;

        public static string ToBase64(this ReadOnlyMemory<byte> bytes) => Convert.ToBase64String(bytes.Span);

        public static ReadOnlySequence<byte> ToSequence(this IEnumerable<ReadOnlyMemory<byte>> segments)
            => (segments?.Count() ?? 0) switch
            {
                0 => ReadOnlySequence<byte>.Empty,
                1 => new ReadOnlySequence<byte>(segments.First()),
                _ => LinkedSegment.Link(segments)
            };

        private class LinkedSegment : ReadOnlySequenceSegment<byte>
        {
            public int Length => Memory.Length;

            public long NextRunningIndex => RunningIndex + Length;

            public static ReadOnlySequence<byte> Link(IEnumerable<ReadOnlyMemory<byte>> segments) {
                var first = new LinkedSegment(segments.First(), 0);
                var current = first;
                foreach (var segment in segments.Skip(1)) {
                    var next = new LinkedSegment(segment, current.NextRunningIndex);
                    current.Next = next;
                    current = next;
                }
                return new ReadOnlySequence<byte>(first, 0, current, current.Length);
            }

            private LinkedSegment(ReadOnlyMemory<byte> memory, long runningIndex) {
                Memory = memory;
                RunningIndex = runningIndex;
            }
        }
    }
}