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
        public abstract string DefaultAddress { get; }
        public abstract int DefaultListeningBufferSize { get; }
        public abstract int DefaultPort { get; }
        public abstract IEnumerable<string> LocalResources { get; }
        public abstract ulong MessageTag { get; }
        public abstract string NetworkName { get; }
        public abstract string NetworkProtocolName { get; }
        public abstract string NodeId { get; }
        public abstract IEnumerable<string> SupportedNetworkProtocolFeatures { get; }

        public abstract void PublishedAs(string address, int tcpPort);

        public abstract Task<Success> SinkAsNodeAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<Response> respond);
    }

    public abstract class Responder
    {
        public void Respond(Response response) {
            if (!response.Exit) {
                SendResponse(response.DataList);
            }
        }

        protected abstract void SendResponse(IList<ArraySegment<byte>> responseSegments);
    }
}