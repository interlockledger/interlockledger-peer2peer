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
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class Locker
    {
        public Locker(CancellationTokenSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public void WithLock(Action action) => WithLockAsync(action).Wait();
        public T WithLock<T>(Func<T> action) => WithLockAsync(() => Task.FromResult( action())).Result;

        public async Task<T> WithLockAsync<T>(Func<Task<T>> action) {
            if (1 == Interlocked.Exchange(ref _locked, 1))
                await Task.Delay(10, _source.Token);
            try {
                return await action();
            } finally {
                Interlocked.Exchange(ref _locked, 0);
            }
        }

        public async Task WithLockAsync(Func<Task> action) {
            if (1 == Interlocked.Exchange(ref _locked, 1))
                await Task.Delay(10, _source.Token);
            try {
                await action();
            } finally {
                Interlocked.Exchange(ref _locked, 0);
            }
        }

        public async Task WithLockAsync(Action action) {
            if (1 == Interlocked.Exchange(ref _locked, 1))
                await Task.Delay(10, _source.Token);
            try {
                action();
            } finally {
                Interlocked.Exchange(ref _locked, 0);
            }
        }

        private readonly CancellationTokenSource _source;
        private int _locked = 0;
    }
}
