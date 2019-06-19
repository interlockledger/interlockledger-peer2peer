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
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public abstract class ListenerCommon : ListenerBase, IListener, IChannelSink
    {
        public bool Alive => _listenSocket != null;

        public abstract Task<Success> SinkAsync(byte[] message, IActiveChannel channel);

        public void Start() {
            if (_source.IsCancellationRequested)
                return;
            Listen().RunOnThread(GetType().FullName);
        }

        public override void Stop() {
            if (!_source.IsCancellationRequested)
                _source.Cancel();
            if (Alive) {
                LogHeader("Stopped");
                try {
                    _listenSocket.Close(10);
                } catch (ObjectDisposedException e) {
                    _logger.LogTrace(e, "ObjectDisposedException");
                }
                _listenSocket = null;
            }
        }

        protected ListenerCommon(string id, ulong tag, CancellationTokenSource source, ILogger logger, int defaultListeningBufferSize)
            : base(id, tag, source, logger, defaultListeningBufferSize) { }

        protected virtual Func<Socket, Task<ISocket>> AcceptSocket => async (socket) => new NetSocket(await socket.AcceptAsync());
        protected abstract string HeaderText { get; }
        protected abstract string IdPrefix { get; }

        protected abstract Socket BuildSocket();

        protected void LogHeader(string verb) => _logger.LogInformation($"-- {verb} " + HeaderText);

        private long _lastIdUsed = 0;
        private Socket _listenSocket;

        private string BuildId() {
            var id = (ulong)Interlocked.Increment(ref _lastIdUsed);
            return $"{IdPrefix}Client{id}";
        }

        private async Task Listen() {
            LogHeader("Started");
            _listenSocket = BuildSocket();
            do {
                try {
                    while (!_source.IsCancellationRequested) {
                        RunPeerClient(await AcceptSocket(_listenSocket));
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
        }

        private void RunPeerClient(ISocket socket)
           => new ConnectionInitiatedByPeer(BuildId(), MessageTag, socket, this, _source, _logger, ListeningBufferSize);
    }
}