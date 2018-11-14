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
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    public sealed class PeerServices : IPeerServices
    {
        public PeerServices(ILoggerFactory loggerFactory, IExternalAccessDiscoverer discoverer) {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            _cache = new Dictionary<string, (string address, int port)>();
        }

        public void AddKnownNode(string nodeId, string address, int port, bool retain = false) {
            if (!_disposedValue) {
                if (string.IsNullOrWhiteSpace(nodeId))
                    throw new ArgumentNullException(nameof(nodeId));
                if (string.IsNullOrWhiteSpace(address))
                    throw new ArgumentNullException(nameof(address));
                _cache[nodeId] = (address, port);
            }
        }

        public IListener CreateFor(INodeSink nodeSink, CancellationTokenSource source)
            => Do(() => new PeerListener(nodeSink, _discoverer, source, CreateLogger(nameof(PeerListener))));

        public void Dispose() {
            if (!_disposedValue) {
                _loggerFactory.Dispose();
                _discoverer.Dispose();
                _cache.Clear();
                _disposedValue = true;
            }
        }

        public IClient GetClient(ulong messageTag, string nodeId, CancellationTokenSource source)
            => Do(() => _cache.TryGetValue(nodeId, out (string address, int port) n) ? GetClient(messageTag, nodeId, n.address, n.port, source) : null);

        public IClient GetClient(ulong messageTag, string id, string address, int port, CancellationTokenSource source)
            => Do(() => new PeerClient(id, address, port, messageTag, source, CreateLogger(nameof(PeerClient))));

        public bool IsNodeKnown(string nodeId) => _cache.ContainsKey(nodeId);

        private readonly IDictionary<string, (string address, int port)> _cache;

        private readonly IExternalAccessDiscoverer _discoverer;

        private readonly ILoggerFactory _loggerFactory;

        private bool _disposedValue = false;

        private ILogger CreateLogger(string categoryName)
            => Do(() => { try { return _loggerFactory.CreateLogger(categoryName); } catch (ObjectDisposedException) { return null; } });

        private T Do<T>(Func<T> func) => _disposedValue ? default : func();
    }
}