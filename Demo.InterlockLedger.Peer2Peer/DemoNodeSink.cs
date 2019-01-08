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
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal class DemoNodeSink : AbstractNodeSink, IClientSink
    {
        public DemoNodeSink() {
            PublishAtAddress= HostAtAddress = "localhost";
            PublishAtPortNumber = HostAtPortNumber = 8080;
            DefaultListeningBufferSize = 512;
            MessageTag = _messageTagCode;
            NetworkName = "Demo";
            NetworkProtocolName = "DemoPeer2Peer";
            NodeId = "Local Node";
        }

        public override IEnumerable<string> LocalResources { get; } = new string[] { "Document" };
        public string Prompt => "Command (x to exit, w to get who is answering, e... to echo ..., 3... to echo ... 3 times): ";
        public override IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "TripleEcho" };
        public string Url => $"demo://{PublishAtAddress}:{PublishAtPortNumber}/";
        public bool WaitForever => false;

        public override void HostedAt(string address, ushort port) {
            HostAtAddress = address;
            HostAtPortNumber = port;
        }

        public override void PublishedAt(string address, ushort port) {
            PublishAtAddress = address;
            PublishAtPortNumber = port;
        }

        public async Task<Success> SinkAsClientAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes) {
            await Task.Delay(1);
            var bytes = readOnlyBytes.SelectMany(m => m.ToArray()).ToArray();
            if (bytes.Length > 1) {
                Console.WriteLine(Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1));
                return bytes[0] == 0 ? Success.Exit : Success.None;
            }
            return Success.Exit;
        }

        public override async Task<Success> SinkAsNodeAsync(IEnumerable<ReadOnlyMemory<byte>> readOnlyBytes, Action<Response> respond) {
            var result = SinkAsServer(readOnlyBytes.SelectMany(b => b.ToArray()).ToArray());
            foreach (var r in result) {
                try {
                    respond(new Response(r));
                } catch (SocketException) {
                    // Do Nothing
                }
                await Task.Delay(1000);
            }
            respond(Response.Done);
            return Success.Exit;
        }

        public IList<ArraySegment<byte>> ToMessage(IEnumerable<byte> bytes, bool isLast)
            => new List<ArraySegment<byte>> { new ArraySegment<byte>(ToMessageBytes(bytes, isLast)) };

        private const ulong _messageTagCode = ':';
        private static readonly IEnumerable<byte> _haveMoreMarker = new byte[] { 1 };
        private static readonly IEnumerable<byte> _isLastMarker = new byte[] { 0 };
        private readonly byte[] _encodedMessageTag = _messageTagCode.ILIntEncode();

        private ReadOnlyMemory<byte> SendResponse(IEnumerable<byte> buffer, bool isLast)
                => new ReadOnlyMemory<byte>(ToMessageBytes(buffer.ToArray(), isLast));

        private ReadOnlyMemory<byte> SendTextResponse(string text, bool isLast) => SendResponse(text.AsUTF8Bytes(), isLast);

        private IEnumerable<ReadOnlyMemory<byte>> SinkAsServer(byte[] buffer) {
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
                yield return SendTextResponse(Url, isLast: true);
                break;

            default:
                yield return SendTextResponse("?", isLast: true);
                break;
            }
        }

        private byte[] ToMessageBytes(IEnumerable<byte> bytes, bool isLast) {
            var prefixedBytes = (isLast ? _isLastMarker : _haveMoreMarker).Concat(bytes);
            var messagebytes = _encodedMessageTag.Concat(((ulong)prefixedBytes.Count()).ILIntEncode()).Concat(prefixedBytes).ToArray();
            return messagebytes;
        }
    }
}