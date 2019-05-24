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

using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal abstract class BaseListener
    {
        public string Id { get; }
        public bool Disposed { get; private set; } = false;

        public void Dispose() {
            if (!Disposed) {
                Stop();
                Disposed = true;
            }
        }

        public abstract void PipelineStopped();

        public abstract void Stop();

        protected internal readonly ILogger _logger;
        protected internal readonly CancellationTokenSource _source;

        protected internal abstract Task<Success> SinkAsync(NetworkMessageSlice slice, IResponder responder);

        protected readonly ulong _messageTag;
        protected readonly int _minimumBufferSize;

        protected BaseListener(string id, ulong tag, CancellationTokenSource source, ILogger logger, int defaultListeningBufferSize) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            Id = id;
            _messageTag = tag;
            _source.Token.Register(Dispose);
            _minimumBufferSize = Math.Max(512, defaultListeningBufferSize);
        }

        protected bool Abandon => _source.IsCancellationRequested || Disposed;
    }
}
