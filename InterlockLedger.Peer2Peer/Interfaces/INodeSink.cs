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
    public enum Success
    {
        None = 0,
        Retry = 1,
        SwitchToListen = 4,
        Exit = 128
    }

    public interface IClientSink
    {
        int DefaultListeningBufferSize { get; }
        bool WaitForever { get; }

        Task<Success> SinkAsClientAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes);
    }

    public interface INodeSink
    {
        int DefaultListeningBufferSize { get; }
        string HostAtAddress { get; }
        ushort HostAtPortNumber { get; }
        IEnumerable<string> LocalResources { get; }
        ulong MessageTag { get; }
        string NetworkName { get; }
        string NetworkProtocolName { get; }
        string NodeId { get; }
        string PublishAtAddress { get; }
        ushort? PublishAtPortNumber { get; }
        IEnumerable<string> SupportedNetworkProtocolFeatures { get; }

        void HostedAt(string address, ushort port);

        void PublishedAt(string address, ushort port);

        Task<Success> SinkAsNodeAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<Response> respond);
    }
}