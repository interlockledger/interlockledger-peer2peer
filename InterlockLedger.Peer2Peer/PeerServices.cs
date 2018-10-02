/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class PeerServices
    {
        public PeerServices(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        public Task<IListener> StartAsync(INodeSink nodeSink) {
            if (nodeSink == null)
                throw new ArgumentNullException(nameof(nodeSink));
            return DoStartAsync(nodeSink, _loggerFactory.CreateLogger<PeerServices>());
        }

        private readonly ILoggerFactory _loggerFactory;

        private async Task<IListener> DoStartAsync(INodeSink nodeSink, ILogger<PeerServices> logger) {
            logger.LogInformation($"Starting listener for {nodeSink.NetworkName} network on {nodeSink.NetworkProtocolName} protocol!");
            return await PeerListener.Create(nodeSink, logger);
        }
    }
}