/******************************************************************************************************************************

Copyright (c) 2018-2021 InterlockLedger Network
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of the copyright holder nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

******************************************************************************************************************************/

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using InterlockLedger.Peer2Peer;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal class DemoServer : DemoBaseSink
    {
        public DemoServer() : base("Server") {
        }

        public string Url => $"demo://{PublishAtAddress}:{PublishAtPortNumber}/";

        public override Task<Success> SinkAsync(ReadOnlySequence<byte> messageBytes, IActiveChannel channel) {
            _queue.Enqueue((messageBytes, channel));
            return Task.FromResult(Success.Next);
        }

        protected override Func<ReadOnlySequence<byte>> AliveMessageBuilder { get; }

        protected override void Run(IPeerServices peerServices) {
            using var listener = (peerServices ?? throw new ArgumentNullException(nameof(peerServices))).CreateListenerFor(this);
            try {
                _ = listener.Start();
                listener.ExcessConnectionRejected += Listener_ExcessConnectionRejected;
                listener.InactiveConnectionDropped += Listener_InactiveConnectionDropped;
                Dequeue().RunOnThread("DemoServer-DelayedResponses");
                while (listener.Alive) {
                    Thread.Sleep(1);
                }
            } finally {
                _stop = true;
            }
        }

        private readonly ConcurrentQueue<(ReadOnlySequence<byte> message, IActiveChannel activeChannel)> _queue = new();

        private bool _stop = false;

        private static ReadOnlySequence<byte> FormatResponse(ReadOnlySequence<byte> buffer, bool isLast)
            => ToMessageBytes(buffer, isLast);

        private static ReadOnlySequence<byte> FormatTextResponse(string text, bool isLast)
            => FormatResponse(AsUTF8Bytes(text), isLast);

        private async Task Dequeue() {
            do {
                if (_queue.TryDequeue(out var tuple))
                    await SinkAsServerWithDelayedResponsesAsync(tuple.message, tuple.activeChannel);
                await Task.Delay(10);
            } while (!_stop);
        }

        private void Listener_ExcessConnectionRejected() => Console.WriteLine("Excess Connection Rejected");

        private void Listener_InactiveConnectionDropped() => Console.WriteLine("Inactive Connection Dropped");

        private async Task SinkAsServerWithDelayedResponsesAsync(ReadOnlySequence<byte> message, IActiveChannel activeChannel) {
            var results = SinkAsServer(message, activeChannel.Channel, out bool silent);
            var channel = activeChannel.Channel;
            if (results.Count() == 1)
                await SendAsync(activeChannel, results.First());
            else {
                foreach (var r in results) {
                    await SendAsync(activeChannel, r);
                    await Task.Delay(2_000);
                }
            }
            if (!silent)
                Console.WriteLine($"Done processing on channel {channel}");

            static async Task SendAsync(IActiveChannel channel, ReadOnlySequence<byte> bytes) {
                try {
                    await channel.SendAsync(bytes);
                } catch (SocketException) {
                    // Do Nothing
                }
            }

            IEnumerable<ReadOnlySequence<byte>> SinkAsServer(ReadOnlySequence<byte> channelBytes, ulong channel, out bool silent) {
                var buffer = channelBytes;
                var command = (buffer.Length > 1) ? (char)buffer.First.Span[1] : '\0';
                silent = command == 'l';
                return ProcessCommand(command, buffer.Slice(2), channel, silent).ToArray();
            }

            IEnumerable<ReadOnlySequence<byte>> ProcessCommand(char command, ReadOnlySequence<byte> text, ulong channel, bool silent) {
                if (!silent)
                    Console.WriteLine($"Received command '{command}' on channel {channel}");
                switch (command) {
                case 'l': // liveness ping
                    yield return FormatTextResponse("Alive", isLast: true);
                    break;

                case 'e':  // is echo message?
                    yield return FormatResponse(text, isLast: true);
                    break;

                case '3': // is triple echo message?
                    var echo = AsString(text);
                    yield return FormatTextResponse($"{echo}:1", isLast: false);
                    yield return FormatTextResponse($"{echo}:2", isLast: false);
                    yield return FormatTextResponse($"{echo}:3", isLast: true);
                    break;

                case 'w':  // is who message?
                    yield return FormatTextResponse(Url, isLast: true);
                    break;

                case '4':
                    var echo4 = AsString(text);
                    yield return FormatTextResponse($"{echo4}|1", isLast: false);
                    yield return FormatTextResponse($"{echo4}|2", isLast: false);
                    yield return FormatTextResponse($"{echo4}|3", isLast: false);
                    yield return FormatTextResponse($"{echo4}|4", isLast: true);
                    break;

                default:
                    yield return FormatTextResponse("?", isLast: true);
                    break;
                }
            }
        }
    }
}