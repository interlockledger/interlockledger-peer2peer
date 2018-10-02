/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System.Collections.Generic;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface INodeSink
    {
        int DefaultPort { get; }
        IEnumerable<string> LocalResources { get; }
        string NetworkName { get; }
        string NetworkProtocolName { get; }
        string NodeId { get; }
        IEnumerable<string> SupportedNetworkProtocolFeatures { get; }

        void PublishedAs(string address, int tcpPort);

        Task SinkAsync(IPipeLine pipe);
    }
}