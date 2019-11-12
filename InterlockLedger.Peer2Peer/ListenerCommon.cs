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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public abstract class ListenerCommon : ListenerBase, IListener, IChannelSink
    {
        public bool Alive => _listenSocket != null;

        public string ExternalAddress { get; protected set; }

        public ushort ExternalPortNumber { get; protected set; }

        public abstract Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel);

        public IListener Start() => StartAsync().Result;

        public Task<IListener> StartAsync() {
            if (!_source.IsCancellationRequested)
                Listen().RunOnThread(GetType().FullName);
            return Task.FromResult<IListener>(this);
        }

        public override void Stop() {
            if (!_source.IsCancellationRequested)
                _source.Cancel();
        }

        protected ListenerCommon(string id, INetworkConfig config, CancellationTokenSource source, ILogger logger)
            : base(id, config, source, logger) { }

        protected virtual Func<Socket, Task<ISocket>> AcceptSocket => async (socket) => new NetSocket(await socket.AcceptAsync());
        protected abstract string HeaderText { get; }
        protected abstract string IdPrefix { get; }

        protected abstract Socket BuildSocket();

        protected override void DisposeManagedResources() {
            if (_listenSocket != null) {
                try {
                    _listenSocket.Close(10);
                } catch (ObjectDisposedException e) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                }
                _listenSocket.Dispose();
                _listenSocket = null;
            }
            foreach (var conn in _connections.ToArray())
                conn?.Dispose();
            _connections.Clear();
            base.DisposeManagedResources();
        }

        protected void LogHeader(string verb) => _logger.LogInformation($"-- {verb} " + HeaderText);

        private readonly List<ConnectionInitiatedByPeer> _connections = new List<ConnectionInitiatedByPeer>();
        private long _lastIdUsed = 0;

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = DisposedJustification)]
        private Socket _listenSocket;

        private string BuildId() => $"{IdPrefix}Client#{(ulong)Interlocked.Increment(ref _lastIdUsed)}";

        private async Task Listen() {
            LogHeader("Started");
            _listenSocket = BuildSocket();
            try {
                do {
                    try {
                        while (!_source.IsCancellationRequested) {
                            _connections.Add(RunPeerClient(await AcceptSocket(_listenSocket)));
                        }
                    } catch (AggregateException e) when (e.InnerExceptions.Any(ex => ex is ObjectDisposedException)) {
                        _logger.LogTrace(e, "ObjectDisposedException");
                    } catch (ObjectDisposedException e) {
                        _logger.LogTrace(e, "ObjectDisposedException");
                    } catch (SocketException e) {
                        _logger.LogTrace(e, $"-- Socket was killed");
                        break;
                    } catch (Exception e) {
                        _logger.LogError(e, $"-- Error while trying to listen.");
                    }
                } while (!_source.IsCancellationRequested);
            } finally {
                LogHeader("Stopped");
                Dispose();
            }
        }

        private ConnectionInitiatedByPeer RunPeerClient(ISocket socket) => new ConnectionInitiatedByPeer(BuildId(), this, socket, this, _source, _logger);
    }
}