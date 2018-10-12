/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Peer2Peer;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal class DemoNodeSink : INodeSink
    {
        public string DefaultAddress => "localhost";
        public int DefaultPort => 8080;

        public IEnumerable<string> LocalResources {
            get {
                yield return "Document";
            }
        }

        public ulong MessageTag => ':';
        public string NetworkName => "Demo";
        public string NetworkProtocolName => "DemoPeer2Peer";
        public string NodeId => "Local Node";
        public IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "Stop" };
        public string Url => $"demo://{_address}:{_externalPort}/";

        public void PublishedAs(string address, int tcpPort) {
            _address = address;
            _externalPort = tcpPort;
        }

        public async Task<ReadOnlyMemory<byte>> SinkAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes) {
            var buffer = readOnlyBytes.SelectMany(b => b.ToArray()).ToArray();
            var command = buffer[0];
            if (command == 0x65)  // is echo message?
                return SendResponse(buffer);
            if (command == 0x77)  // is who message?
                return SendTextResponse(Url);
            await Task.Delay(1);
            return SendTextResponse("?");
        }

        private static ReadOnlyMemory<byte> SendTextResponse(string text) => SendResponse(Encoding.UTF8.GetBytes(text));
        private static readonly byte[] _newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

        private string _address;

        private int _externalPort;

        private static ReadOnlyMemory<byte> SendResponse(byte[] buffer) => new ReadOnlyMemory<byte>(buffer.Concat(_newLineBytes).ToArray());
    }
}
