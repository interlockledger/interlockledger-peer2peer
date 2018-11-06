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
    internal class FakeNodeSync : INodeSink
    {
        public string DefaultAddress => "localhost";
        public int DefaultListeningBufferSize => 1024;
        public int DefaultPort => 9090;
        public IEnumerable<string> LocalResources { get; } = new string[] { "DummyDoc1", "DummyDoc2" };
        public ulong MessageTag => '?';
        public string NetworkName => "UnitTesting";
        public string NetworkProtocolName => "UnitTest";
        public string NodeId => "DummyNode";
        public IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "None" };

        public void PublishedAs(string address, int tcpPort) {
            // Do nothing
        }

        public async Task<Success> SinkAsNodeAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<Response> respond) {
            await Task.Delay(10);
            respond(Response.Done);
            return Success.Exit;
        }
    }
}