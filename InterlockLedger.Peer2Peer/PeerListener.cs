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
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal class PeerListener : BaseListener, IListener
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    {
        public PeerListener(INodeSink nodeSink, IExternalAccessDiscoverer discoverer, CancellationTokenSource source, ILogger logger)
            : base(nodeSink.NodeId, nodeSink.MessageTag, source, logger, nodeSink.DefaultListeningBufferSize) {
            _nodeSink = nodeSink ?? throw new ArgumentNullException(nameof(nodeSink));
            _listenSocket = DetermineExternalAccess(discoverer ?? throw new ArgumentNullException(nameof(discoverer)));
        }

        public bool Alive => _listenSocket != null;

        public override void PipelineStopped() {
            // Do Nothing
        }

        public override Task<Success> SinkAsync(NetworkMessageSlice slice, IResponder responder) => _nodeSink.SinkAsync(slice, responder);

        public void Start() {
            if (_source.IsCancellationRequested)
                return;
            Listen().RunOnThread(nameof(PeerListener));
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

        protected void LogHeader(string verb)
            => _logger.LogInformation($"-- {verb} listening {_nodeSink.NetworkProtocolName} protocol in {_nodeSink.NetworkName} network at {_externalAccess.Route}!");

        private readonly INodeSink _nodeSink;
        private ExternalAccess _externalAccess;
        private Socket _listenSocket;

        private Socket DetermineExternalAccess(IExternalAccessDiscoverer _discoverer) {
            _externalAccess = _discoverer.DetermineExternalAccessAsync(_nodeSink).Result;
            _nodeSink.HostedAt(_externalAccess.InternalAddress, _externalAccess.InternalPort);
            _nodeSink.PublishedAt(_externalAccess.ExternalAddress, _externalAccess.ExternalPort);
            return _externalAccess.Socket;
        }

        private async Task Listen() {
            LogHeader("Started");
            do {
                try {
                    while (!_source.IsCancellationRequested) {
                        RunPeerClient(await _listenSocket.AcceptAsync());
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

        private ListenerClient RunPeerClient(Socket socket)
            => new ListenerClient("ListenerClient", _nodeSink.MessageTag, socket, this, _nodeSink.DefaultListeningBufferSize);
    }
}