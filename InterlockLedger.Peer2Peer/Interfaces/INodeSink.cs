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
    public interface INodeSink
    {
        string DefaultAddress { get; }
        int DefaultPort { get; }
        IEnumerable<string> LocalResources { get; }
        ulong MessageTag { get; }
        string NetworkName { get; }
        string NetworkProtocolName { get; }
        string NodeId { get; }
        IEnumerable<string> SupportedNetworkProtocolFeatures { get; }

        void PublishedAs(string address, int tcpPort);

        Task<ReadOnlyMemory<byte>> SinkAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes);
    }
}
