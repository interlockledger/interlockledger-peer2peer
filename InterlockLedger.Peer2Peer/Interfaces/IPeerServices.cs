/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Threading;

namespace InterlockLedger.Peer2Peer
{
    public interface IPeerServices : IDisposable
    {
        void AddKnownNode(string nodeId, string address, int port, bool retain = false);

        IListener CreateFor(INodeSink nodeSink, CancellationTokenSource source);

        IClient GetClient(ulong messageTag, string id, string address, int port, CancellationTokenSource source);

        IClient GetClient(ulong messageTag, string nodeId, CancellationTokenSource source);

        bool IsNodeKnown(string nodeId);
    }
}