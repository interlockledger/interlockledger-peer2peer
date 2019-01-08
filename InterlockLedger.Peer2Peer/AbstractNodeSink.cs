/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public abstract class AbstractNodeSink : INodeSink
    {
        public int DefaultListeningBufferSize { get; protected set; }
        public string HostAtAddress { get; protected set; }
        public ushort HostAtPortNumber { get; protected set; }
        public abstract IEnumerable<string> LocalResources { get; }
        public ulong MessageTag { get; protected set; }
        public string NetworkName { get; protected set; }
        public string NetworkProtocolName { get; protected set; }
        public string NodeId { get; protected set; }
        public string PublishAtAddress { get; protected set; }
        public ushort? PublishAtPortNumber { get; protected set; }
        public abstract IEnumerable<string> SupportedNetworkProtocolFeatures { get; }

        public abstract void HostedAt(string address, ushort port);

        public abstract void PublishedAt(string address, ushort port);

        public abstract Task<Success> SinkAsNodeAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<Response> respond);
    }
}