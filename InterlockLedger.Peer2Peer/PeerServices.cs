// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2023 InterlockLedger Network
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

using System.Collections.Concurrent;

namespace InterlockLedger.Peer2Peer
{
    public sealed class PeerServices : AbstractDisposable, IPeerServices, IKnownNodesServices, IProxyingServices
    {
        public PeerServices(ulong messageTag,
                            ulong livenessMessageTag,
                            string networkName,
                            string networkProtocolName,
                            int listeningBufferSize,
                            ILoggerFactory loggerFactory,
                            IExternalAccessDiscoverer discoverer,
                            SocketFactory socketFactory,
                            int inactivityTimeoutInMinutes,
                            int maxConcurrentConnections) {
            if (messageTag == livenessMessageTag)
                throw new ArgumentException($"{nameof(livenessMessageTag)} (value: {livenessMessageTag}) must be different from {nameof(messageTag)} (value: {messageTag})! ");
            MessageTag = messageTag;
            LivenessMessageTag = livenessMessageTag;
            NetworkName = networkName.Required(nameof(networkName));
            NetworkProtocolName = networkProtocolName.Required(nameof(networkProtocolName));
            ListeningBufferSize = Math.Max(512, listeningBufferSize);
            InactivityTimeoutInMinutes = Math.Max(inactivityTimeoutInMinutes, 0);
            _loggerFactory = loggerFactory.Required(nameof(loggerFactory));
            _discoverer = discoverer.Required(nameof(discoverer));
            _socketFactory = socketFactory.Required(nameof(socketFactory));
            _knownNodes = new ConcurrentDictionary<string, (string address, int port, bool retain)>();
            _clients = new ConcurrentDictionary<string, IConnection>();
            _logger = LoggerNamed(nameof(PeerServices));
            MaxConcurrentConnections = Math.Max(maxConcurrentConnections, 0);
        }

        public int InactivityTimeoutInMinutes { get; }
        public IKnownNodesServices KnownNodes => this;
        public int ListeningBufferSize { get; }
        public ulong LivenessMessageTag { get; }
        public int MaxConcurrentConnections { get; }
        public ulong MessageTag { get; }
        public string NetworkName { get; }
        public string NetworkProtocolName { get; }
        public IProxyingServices ProxyingServices => this;
        public CancellationTokenSource Source => _source ?? throw new InvalidOperationException("CancellationTokenSource was not set yet!");

        void IKnownNodesServices.Add(string nodeId, string address, int port, bool retain)
            => Do(() => _knownNodes[nodeId.Required(nameof(nodeId))] = (address.Required(nameof(address)), port, retain));

        void IKnownNodesServices.Add(string nodeId, IConnection connection, bool retain)
            => Do(() => {
                _knownNodes[nodeId] = (nodeId.Required(nameof(nodeId)), 0, retain);
                _clients[Framed(nodeId)] = connection;
            });

        public IListener CreateListenerFor(INodeSink nodeSink)
            => UnsafeDo(() => new ListenerForPeer(nodeSink, _discoverer, Source, LoggerNamed($"{nameof(ListenerForPeer)}#{nodeSink.MessageTag}")));

        IListenerForProxying IProxyingServices.CreateListenerForProxying(string externalAddress, string hostedAddress, ushort firstPort, IConnection connection)
            => UnsafeDo(() => new ListenerForProxying(externalAddress, hostedAddress, firstPort, connection, _socketFactory, _source, LoggerNamed($"{nameof(ListenerForProxying)}#{connection.MessageTag}|from:{connection.Id}")));

        void IKnownNodesServices.Forget(string nodeId) => Do(() => _ = _knownNodes.TryRemove(nodeId, out _));

        public IConnection GetClient(string address, int port) {
            IConnection Lookup() {
                var id = $"{address}:{port}#{MessageTag}";
                try {
                    if (_clients.TryGetValue(id, out var existingClient))
                        if (IsConnected(existingClient))
                            return existingClient;
                        else {
                            _clients.TryRemove(id, out _);
                            existingClient.Dispose();
                        }
                    var newClient = BuildClient(address, port, id);
                    if (_clients.TryAdd(id, newClient))
                        return newClient;
                    newClient.Dispose();
                } catch (Exception e) {
                    _logger.LogError(e, "Could not build PeerClient for {id}!", id);
                }
                return null;
            }
            return UnsafeDo(Lookup);
        }

        public IConnection GetClient(string nodeId)
            => UnsafeDo(() => _clients.TryGetValue(Framed(nodeId), out var existingClient) ? existingClient : null);

        IConnection IKnownNodesServices.GetClient(string nodeId) => UnsafeDo(() => GetResponder(nodeId));

        IConnection IProxyingServices.GetClientForProxying(string address, int port)
            => BuildClient(address, port, $"{address}:{port}#{MessageTag}/proxying");

        bool IKnownNodesServices.IsKnown(string nodeId) => Do(() => _knownNodes.ContainsKey(nodeId));

        public IPeerServices WithCancellationTokenSource(CancellationTokenSource source) {
            _source = source.Required(nameof(source));
            return this;
        }

        protected override void DisposeManagedResources() {
            _loggerFactory.Dispose();
            _discoverer.Dispose();
            _knownNodes.Clear();
            foreach (var client in _clients.Values)
                client?.Dispose();
            _clients.Clear();
            ErrorCachingLogger.StopBackgroundProcessing();
        }

        private readonly ConcurrentDictionary<string, IConnection> _clients;
        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly ConcurrentDictionary<string, (string address, int port, bool retain)> _knownNodes;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SocketFactory _socketFactory;
        private CancellationTokenSource _source;

        private static string Framed(string nodeId) => $"[{nodeId}]";

        private ConnectionToPeer BuildClient(string address, int port, string id)
            => new(id, this, address, port, Source, LoggerForClient(id));

        private IConnection GetResponder(string nodeId)
            => _knownNodes.TryGetValue(nodeId, out var n) ? n.port != 0 ? GetClient(n.address, n.port) : GetClient(nodeId) : null;

        private bool IsConnected(IConnection existingClient) {
            try {
                return Do(() => existingClient.Connected);
            } catch {
                return false;
            }
        }

        private ILogger LoggerForClient(string id) => LoggerNamed($"{nameof(ConnectionToPeer)}@{id}");

        private ILogger LoggerNamed(string categoryName) => UnsafeDo(() => _loggerFactory.CreateLogger(categoryName));
    }
}