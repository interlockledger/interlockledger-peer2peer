/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace InterlockLedger.Peer2Peer
{
    public class PeerServices
    {
        public PeerServices(ILoggerFactory loggerFactory, IExternalAccessDiscoverer discoverer) {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
        }

        public IListener CreateFor(INodeSink nodeSink, CancellationTokenSource source) {
            if (nodeSink == null)
                throw new ArgumentNullException(nameof(nodeSink));
            return new PeerListener(nodeSink, _loggerFactory.CreateLogger("PeerListener"), _discoverer, source);
        }

        private readonly IExternalAccessDiscoverer _discoverer;
        private readonly ILoggerFactory _loggerFactory;
    }
}