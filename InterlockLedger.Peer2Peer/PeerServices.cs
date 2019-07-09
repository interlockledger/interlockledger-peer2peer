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
using System.Collections.Concurrent;
using System.Threading;

namespace InterlockLedger.Peer2Peer
{
    public sealed class PeerServices : IPeerServices, IKnownNodesServices, IProxyingServices
    {
        public PeerServices(ulong messageTag, string networkName, string networkProtocolName, int listeningBufferSize, ILoggerFactory loggerFactory, IExternalAccessDiscoverer discoverer, SocketFactory socketFactory) {
            _disposer = new Disposer();
            MessageTag = messageTag;
            NetworkName = networkName ?? throw new ArgumentNullException(nameof(networkName));
            NetworkProtocolName = networkProtocolName ?? throw new ArgumentNullException(nameof(networkProtocolName));
            ListeningBufferSize = Math.Max(512, listeningBufferSize);
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            _socketFactory = socketFactory ?? throw new ArgumentNullException(nameof(socketFactory));
            _knownNodes = new ConcurrentDictionary<string, (string address, int port, bool retain)>();
            _clients = new ConcurrentDictionary<string, IConnection>();
            _logger = LoggerNamed(nameof(PeerServices));
        }

        public IKnownNodesServices KnownNodes => this;
        public int ListeningBufferSize { get; }
        public ulong MessageTag { get; }
        public string NetworkName { get; }
        public string NetworkProtocolName { get; }
        public IProxyingServices ProxyingServices => this;
        public CancellationTokenSource Source => _source ?? throw new InvalidOperationException("CancellationTokenSource was not set yet!");

        public IListener CreateListenerFor(INodeSink nodeSink)
            => _disposer.Do(() => new ListenerForPeer(nodeSink, _discoverer, Source, LoggerNamed($"{nameof(ListenerForPeer)}#{nodeSink.MessageTag}")));

        public void Dispose()
            => _disposer.Dispose(() => {
                _loggerFactory.Dispose();
                _discoverer.Dispose();
                _knownNodes.Clear();
                foreach (var client in _clients.Values)
                    client?.Dispose();
                _clients.Clear();
            });

        public IConnection GetClient(string address, int port)
            => _disposer.Do(() => {
                var id = $"{address}:{port}#{MessageTag}";
                try {
                    if (_clients.TryGetValue(id, out var existingClient))
                        return existingClient;
                    ConnectionToPeer client = BuildClient(address, port, id);
                    if (_clients.TryAdd(id, client))
                        return client;
                    client.Dispose();
                } catch (Exception e) {
                    _logger.LogError(e, "Could not build PeerClient for {0}!", id);
                }
                return null;
            });

        public IConnection GetClient(string nodeId)
            => _disposer.Do(() => _clients.TryGetValue(Framed(nodeId), out var existingClient) ? existingClient : null);

        public IPeerServices WithCancellationTokenSource(CancellationTokenSource source) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        void IKnownNodesServices.Add(string nodeId, string address, int port, bool retain)
            => _disposer.Do(() => {
                if (string.IsNullOrWhiteSpace(nodeId))
                    throw new ArgumentNullException(nameof(nodeId));
                if (string.IsNullOrWhiteSpace(address))
                    throw new ArgumentNullException(nameof(address));
                _knownNodes[nodeId] = (address, port, retain);
            });

        void IKnownNodesServices.Add(string nodeId, IConnection connection, bool retain)
            => _disposer.Do(() => {
                if (string.IsNullOrWhiteSpace(nodeId))
                    throw new ArgumentNullException(nameof(nodeId));
                _knownNodes[nodeId] = (nodeId, 0, retain);
                _clients[Framed(nodeId)] = connection;
            });

        IListenerForProxying IProxyingServices.CreateListenerForProxying(string externalAddress, ushort firstPort, IConnection connection)
           => _disposer.Do(() => new ListenerForProxying(externalAddress, firstPort, connection, _socketFactory, Source, LoggerNamed($"{nameof(ListenerForProxying)}[{connection.Id}]#{connection.MessageTag}")));

        void IKnownNodesServices.Forget(string nodeId) => _disposer.Do(() => { _ = _knownNodes.TryRemove(nodeId, out _); });

        IConnection IKnownNodesServices.GetClient(string nodeId) => _disposer.Do(() => GetResponder(nodeId));

        IConnection IProxyingServices.GetClientForProxying(string address, int port)
            => BuildClient(address, port, $"{address}:{port}#{MessageTag}/proxying");

        bool IKnownNodesServices.IsKnown(string nodeId) => _disposer.Do(() => _knownNodes.ContainsKey(nodeId));

        private readonly ConcurrentDictionary<string, IConnection> _clients;
        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly Disposer _disposer;
        private readonly ConcurrentDictionary<string, (string address, int port, bool retain)> _knownNodes;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SocketFactory _socketFactory;
        private CancellationTokenSource _source;

        private static string Framed(string nodeId) => $"[{nodeId}]";

        private ConnectionToPeer BuildClient(string address, int port, string id)
            => new ConnectionToPeer(id, this, address, port, Source, LoggerForClient(id));

        private IConnection GetResponder(string nodeId)
            => _knownNodes.TryGetValue(nodeId, out (string address, int port, bool retain) n)
                ? n.port != 0 ? GetClient(n.address, n.port) : GetClient(nodeId)
                : null;

        private ILogger LoggerForClient(string id) => LoggerNamed($"{nameof(ConnectionToPeer)}@{id}");

        private ILogger LoggerNamed(string categoryName) => _disposer.Do(() => _loggerFactory.CreateLogger(categoryName));
    }
}