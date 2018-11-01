/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Common;
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
        public DemoNodeSink() => _processor = new ProtocolProcessor(this);

        public IMessageProcessor ClientProcessor => _processor;
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
        public string Url => $"demo://{_address}:{_externalPort}/";
        public string Prompt => _processor.Prompt;
        public IEnumerable<string> SupportedNetworkProtocolFeatures => _processor.SupportedNetworkProtocolFeatures;

        public void PublishedAs(string address, int tcpPort) {
            _address = address;
            _externalPort = tcpPort;
        }

        public async Task SinkAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<ReadOnlyMemory<byte>, bool> respond) {
            await Task.Delay(1);
            var result = _processor.Sink(readOnlyBytes.SelectMany(b => b.ToArray()).ToArray());
            foreach (var r in result) {
                respond(r, false);
                await Task.Delay(1000);
            }
            respond(default, true);
        }

        public Span<byte> ToMessage(IEnumerable<byte> bytes, bool isLast) => _processor.ToMessage(bytes, isLast);

        private readonly ProtocolProcessor _processor;
        private string _address;
        private int _externalPort;

        private class ProtocolProcessor : IMessageProcessor
        {
            public ProtocolProcessor(DemoNodeSink demoNode) => _demoNode = demoNode ?? throw new ArgumentNullException(nameof(demoNode));

            public bool AwaitMultipleAnswers => true;
            public string Prompt => "Command (x to exit, w to get who is answering, e... to echo ..., 3... to echo ... 3 times): ";
            public IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "TripleEcho" };

            public Success Process(List<ReadOnlyMemory<byte>> segments) {
                var bytes = segments.SelectMany(m => m.ToArray()).ToArray();
                if (bytes.Length > 1) {
                    Console.WriteLine(Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1));
                    return bytes[0] == 0 ? (Success.Processed | Success.Exit) : Success.Processed;
                }
                return Success.Processed | Success.Exit;
            }

            public IEnumerable<ReadOnlyMemory<byte>> Sink(byte[] buffer) {
                var command = (buffer.Length > 1) ? (char)buffer[1] : '\0';
                switch (command) {
                case 'e':  // is echo message?
                    yield return SendResponse(buffer.Skip(2), isLast: true);
                    break;
                case '3': // is triple echo message?
                    yield return SendResponse(buffer.Skip(2), isLast: false);
                    yield return SendResponse(buffer.Skip(2), isLast: false);
                    yield return SendResponse(buffer.Skip(2), isLast: true);
                    break;
                case 'w':  // is who message?
                    yield return SendTextResponse(_demoNode.Url, isLast: true);
                    break;
                default:
                    yield return SendTextResponse("?", isLast: true);
                    break;
                }
            }

            internal Span<byte> ToMessage(IEnumerable<byte> bytes, bool isLast) {
                var prefixedBytes = (isLast ? _isLastMarker : _haveMoreMarker).Concat(bytes);
                var messagebytes = EncodedMessageTag.Concat(((ulong)prefixedBytes.Count()).ILIntEncode()).Concat(prefixedBytes).ToArray();
                return messagebytes.AsSpan();
            }

            private byte[] EncodedMessageTag => _demoNode.MessageTag.ILIntEncode();

            private static readonly IEnumerable<byte> _haveMoreMarker = new byte[] { 1 };
            private static readonly IEnumerable<byte> _isLastMarker = new byte[] { 0 };
            private readonly DemoNodeSink _demoNode;

            private ReadOnlyMemory<byte> SendResponse(IEnumerable<byte> buffer, bool isLast)
                => new ReadOnlyMemory<byte>(ToMessage(buffer.ToArray(), isLast).ToArray());

            private ReadOnlyMemory<byte> SendTextResponse(string text, bool isLast) => SendResponse(text.AsUTF8Bytes(), isLast);
        }
    }
}