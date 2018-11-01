/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace InterlockLedger.Peer2Peer
{
    public class PeerServices
    {
        public PeerServices(ILoggerFactory loggerFactory, IExternalAccessDiscoverer discoverer) {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            _cache = new Dictionary<string, (string address, int port)>();
        }

        public void AddKnownNode(string nodeId, string address, int port, bool retain = false) {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentNullException(nameof(nodeId));
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentNullException(nameof(address));
            _cache[nodeId] = (address, port);
        }

        public IListener CreateFor(INodeSink nodeSink, CancellationTokenSource source) {
            if (nodeSink == null)
                throw new ArgumentNullException(nameof(nodeSink));
            return new PeerListener(nodeSink, _loggerFactory.CreateLogger("PeerListener"), _discoverer, source);
        }

        public IClient GetClient(ulong messageTag, string nodeId)
            => _cache.TryGetValue(nodeId, out (string address, int port) n) ? GetClient(nodeId, messageTag, n.address, n.port) : null;

        public IClient GetClient(string id, ulong messageTag, string address, int port)
            => new PeerClient(id, address, port, messageTag, _loggerFactory.CreateLogger("PeerClient"));

        private readonly IDictionary<string, (string address, int port)> _cache;
        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly ILoggerFactory _loggerFactory;
    }
}