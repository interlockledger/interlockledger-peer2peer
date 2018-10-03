using InterlockLedger.Peer2Peer;
using System;
using System.Collections.Generic;
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

        public string NetworkName => "Demo";
        public string NetworkProtocolName => "DemoPeer2Peer";
        public string NodeId => "Local Node";
        public IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "Stop" };
        public string Url => $"demo://{_address}:{_externalPort}/";

        public void PublishedAs(string address, int tcpPort) {
            _address = address;
            _externalPort = tcpPort;
        }

        public async Task SinkAsync(IPipeLine pipe) {
            while (!pipe.ReadNoMore) {
                var bytesRead = await pipe.ReadBytesAsync(100);
                if (bytesRead != null && bytesRead.Length > 0) {
                    if (bytesRead[0] == 0x65) { // is echo message?
                        pipe.RecognizedFeature("Echo");
                        await pipe.WriteBytesAsync(bytesRead, 1, bytesRead.Length - 1);
                        await SendNewLine(pipe);
                        await pipe.CompleteAsync();
                    } else if (bytesRead[0] == 0x77) { // is who message?
                        pipe.RecognizedFeature("Who");
                        await pipe.WriteBytesAsync(Encoding.UTF8.GetBytes(Url));
                        await SendNewLine(pipe);
                        await pipe.CompleteAsync();
                    } else {
                        await pipe.RejectAsync(1); // unrecognized message
                    }
                }
            }
        }

        private string _address;

        private int _externalPort;

        private async Task SendNewLine(IPipeLine pipe) => await pipe.WriteBytesAsync(Encoding.UTF8.GetBytes(Environment.NewLine));
    }
}