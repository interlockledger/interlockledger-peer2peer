// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2021 InterlockLedger Network
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    /// <summary>
    /// Based on Stephen Toub series of posts 'Building Async Coordination Primitives'
    /// </summary>
    internal class AsyncLock
    {
        public AsyncLock() {
            _semaphore = new AsyncSemaphore();
            _releaser = Task.FromResult(new Releaser(this));
        }

        public Task<Releaser> LockAsync() {
            var wait = _semaphore.WaitAsync();
            return wait.IsCompleted ?
                _releaser :
                wait.ContinueWith((_, state) => new Releaser((AsyncLock)state),
                    this, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public struct Releaser : IDisposable
        {
            public void Dispose() => _toRelease?._semaphore.Release();

            internal Releaser(AsyncLock toRelease) => _toRelease = toRelease;

            private readonly AsyncLock _toRelease;
        }

        private readonly Task<Releaser> _releaser;
        private readonly AsyncSemaphore _semaphore;

        private class AsyncSemaphore
        {
            public AsyncSemaphore() => _currentCount = 1;

            public void Release() {
                TaskCompletionSource<bool> toRelease = null;
                lock (_waiters) {
                    if (_waiters.Count > 0)
                        toRelease = _waiters.Dequeue();
                    else
                        _currentCount++;
                }
                toRelease?.SetResult(true);
            }

            public Task WaitAsync() {
                lock (_waiters) {
                    if (_currentCount > 0) {
                        _currentCount--;
                        return _completed;
                    } else {
                        var waiter = new TaskCompletionSource<bool>();
                        _waiters.Enqueue(waiter);
                        return waiter.Task;
                    }
                }
            }

            private static readonly Task _completed = Task.FromResult(true);
            private readonly Queue<TaskCompletionSource<bool>> _waiters = new Queue<TaskCompletionSource<bool>>();
            private int _currentCount;
        }
    }
}