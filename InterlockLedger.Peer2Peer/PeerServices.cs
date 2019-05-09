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
using System.Threading;

namespace InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    public sealed class PeerServices : IPeerServices, IKnownNodesServices
    {
        public PeerServices(ILoggerFactory loggerFactory, IExternalAccessDiscoverer discoverer) {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            _knownNodes = new Dictionary<string, (string address, int port, ulong messageTag, int defaultListeningBufferSize, bool retain)>();
            _clients = new Dictionary<string, IResponder>();
        }

        public IKnownNodesServices KnownNodes => this;
        public CancellationTokenSource Source => _source ?? throw new InvalidOperationException("CancellationTokenSource was not set yet!");

        public IListener CreateListenerFor(INodeSink nodeSink)
            => Do(() => new PeerListener(nodeSink, _discoverer, Source, CreateLogger($"{nameof(PeerListener)}#{nodeSink.MessageTag}")));

        public void Dispose() {
            if (!_disposedValue) {
                _loggerFactory.Dispose();
                _discoverer.Dispose();
                _knownNodes.Clear();
                foreach (var client in _clients.Values)
                    client?.Dispose();
                _clients.Clear();
                _disposedValue = true;
            }
        }

        public IResponder GetClient(ulong messageTag, string address, int port, int defaultListeningBufferSize)
            => Do(() => {
                lock (_clients) {
                    var id = $"{address}:{port}#{messageTag}";
                    if (!_clients.ContainsKey(id)) {
                        _clients.Add(id, new PeerClient(id, messageTag, address, port, Source, CreateLogger($"{nameof(PeerClient)}@{id}"), defaultListeningBufferSize));
                    }
                    return _clients[id];
                }
            });

        public IResponder GetClient(string nodeId)
            => Do(() => {
                lock (_clients) {
                    var id = Prefix(nodeId);
                    if (_clients.ContainsKey(id))
                        return _clients[id];
                    return null;
                }
            });

        public IPeerServices WithCancellationTokenSource(CancellationTokenSource source) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        void IKnownNodesServices.Add(string nodeId, ulong messageTag, string address, int port, int defaultListeningBufferSize, bool retain) {
            if (!_disposedValue) {
                if (string.IsNullOrWhiteSpace(nodeId))
                    throw new ArgumentNullException(nameof(nodeId));
                if (string.IsNullOrWhiteSpace(address))
                    throw new ArgumentNullException(nameof(address));
                _knownNodes[nodeId] = (address, port, messageTag, defaultListeningBufferSize, retain);
            }
        }

        void IKnownNodesServices.Add(string nodeId, IResponder responder, bool retain) {
            if (!_disposedValue) {
                if (string.IsNullOrWhiteSpace(nodeId))
                    throw new ArgumentNullException(nameof(nodeId));
                _knownNodes[nodeId] = (nodeId, 0, 0, 0, retain);
                AddClient(nodeId, responder);
            }
        }

        void IKnownNodesServices.Forget(string nodeId) {
            if ((!_disposedValue) && _knownNodes.ContainsKey(nodeId)) _knownNodes.Remove(nodeId);
        }

        IResponder IKnownNodesServices.GetClient(string nodeId)
        => Do(
            () => _knownNodes.TryGetValue(nodeId, out (string address, int port, ulong messageTag, int defaultListeningBufferSize, bool retain) n)
                    ? n.port != 0 ? GetClient(n.messageTag, n.address, n.port, n.defaultListeningBufferSize) : GetClient(nodeId)
                    : null
            );

        bool IKnownNodesServices.IsKnown(string nodeId) => (!_disposedValue) && _knownNodes.ContainsKey(nodeId);

        private readonly IDictionary<string, IResponder> _clients;

        private readonly IExternalAccessDiscoverer _discoverer;

        private readonly IDictionary<string, (string address, int port, ulong messageTag, int defaultListeningBufferSize, bool retain)> _knownNodes;

        private readonly ILoggerFactory _loggerFactory;

        private bool _disposedValue = false;

        private CancellationTokenSource _source;

        private void AddClient(string nodeId, IResponder responder) {
            lock (_clients) {
                _clients[Prefix(nodeId)] = responder ?? throw new ArgumentNullException(nameof(responder));
            }
        }

        private ILogger CreateLogger(string categoryName) {
            try {
                return _loggerFactory.CreateLogger(categoryName);
            } catch (ObjectDisposedException) { return null; }
        }

        private T Do<T>(Func<T> func) => _disposedValue ? default : func();

        private string Prefix(string nodeId) => $"[{nodeId}]";
    }
}