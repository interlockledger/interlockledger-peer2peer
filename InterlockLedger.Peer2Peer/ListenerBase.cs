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
using System.Threading;
using Microsoft.Extensions.Logging;

namespace InterlockLedger.Peer2Peer
{
    public abstract class ListenerBase : AbstractDisposable, INetworkIdentity
    {
        public string Id { get; }
        public int InactivityTimeoutInMinutes => _config.InactivityTimeoutInMinutes;
        public int ListeningBufferSize => _config.ListeningBufferSize;
        public int MaxConcurrentConnections => _config.MaxConcurrentConnections;
        public ulong MessageTag => _config.MessageTag;
        public string NetworkName => _config.NetworkName;
        public string NetworkProtocolName => _config.NetworkProtocolName;

        public abstract void Stop();

        protected internal readonly ILogger _logger;
        protected internal readonly CancellationTokenSource _source;
        protected internal bool Abandon => _source.IsCancellationRequested || Disposed;

        protected ListenerBase(string id, INetworkConfig config, CancellationTokenSource source, ILogger logger) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            Id = id;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _source.Token.Register(Dispose);
        }

        protected override void DisposeManagedResources() => Stop();

        private readonly INetworkConfig _config;
    }
}