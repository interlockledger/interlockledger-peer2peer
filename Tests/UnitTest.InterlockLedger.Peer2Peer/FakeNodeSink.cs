/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Peer2Peer;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnitTest.InterlockLedger.Peer2Peer
{
    internal class FakeNodeSink : INodeSink
    {
        public int DefaultListeningBufferSize => 1024;
        public string HostAtAddress => "localhost";
        public ushort HostAtPortNumber => 9090;
        public IEnumerable<string> LocalResources { get; } = new string[] { "DummyDoc1", "DummyDoc2" };
        public ulong MessageTag => '?';
        public string NetworkName => "UnitTesting";
        public string NetworkProtocolName => "UnitTest";
        public string NodeId => "DummyNode";
        public string PublishAtAddress => HostAtAddress;
        public ushort? PublishAtPortNumber => HostAtPortNumber;
        public IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "None" };

        public void HostedAt(string address, ushort port) {
            // Do nothing
        }

        public void PublishedAt(string address, ushort port) {
            // Do nothing
        }

        public async Task<Success> SinkAsNodeAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<Response> respond) {
            await Task.Delay(10);
            respond(Response.Done);
            return Success.Exit;
        }
    }
}