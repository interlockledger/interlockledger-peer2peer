/******************************************************************************************************************************

Copyright (c) 2018-2019 InterlockLedger Network
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

using InterlockLedger.Peer2Peer;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal class DemoServer : DemoBaseSink
    {
        public DemoServer(CancellationTokenSource source) : base("Server", source) {
        }

        public string Url => $"demo://{PublishAtAddress}:{PublishAtPortNumber}/";

        public override void Run(IPeerServices peerServices) {
            _peerServices = peerServices ?? throw new ArgumentNullException(nameof(peerServices));
            using (var listener = peerServices.CreateListenerFor(this)) {
                try {
                    listener.Start();
                    Dequeue().RunOnThread("DemoServer-DelayedResponses");
                    DequeueKey().RunOnThread("Keys");
                    while (listener.Alive) {
                        Thread.Sleep(1);
                    }
                } finally {
                    _stop = true;
                }
            }
        }

        public override Task<Success> SinkAsync(ChannelBytes channelBytes, IResponder responder) {
            _queue.Enqueue((channelBytes, responder));
            return Task.FromResult(Success.Next);
        }

        private readonly ConcurrentQueue<(ChannelBytes channelBytes, IResponder responder)> _queue = new ConcurrentQueue<(ChannelBytes channelBytes, IResponder responder)>();
        private readonly ConcurrentQueue<(ulong channel, string key)> _queueKeys = new ConcurrentQueue<(ulong channel, string key)>();

        private IPeerServices _peerServices;

        private bool _stop = false;

        private static void Send(IResponder responder, ulong channel, ReadOnlyMemory<byte> bytes) {
            try {
                responder.Send(new ChannelBytes(channel, bytes));
            } catch (SocketException) {
                // Do Nothing
            }
        }

        private async Task Dequeue() {
            do {
                if (_queue.TryDequeue(out var tuple))
                    await SinkAsServerWithDelayedResponsesAsync(tuple.channelBytes, tuple.responder);
                await Task.Delay(10);
            } while (!_stop);
        }

        private async Task DequeueKey() {
            do {
                if (_queueKeys.TryDequeue(out var tuple))
                    await KeyedSend(tuple.channel, tuple.key);
                await Task.Delay(10);
            } while (!_stop);
        }

        private ReadOnlyMemory<byte> FormatResponse(IEnumerable<byte> buffer, bool isLast)
            => new ReadOnlyMemory<byte>(ToMessageBytes(buffer.ToArray(), isLast));

        private ReadOnlyMemory<byte> FormatTextResponse(string text, bool isLast) => FormatResponse(AsUTF8Bytes(text), isLast);

        private async Task KeyedSend(ulong channel, string key) {
            int i = 0;
            var r = _peerServices.KnownNodes.GetClient(key);
            while (++i < 5) {
                await Task.Delay(1_000);
                var message = FormatTextResponse($"{key}#{i}", isLast: i >= 4);
                _ = r?.Send(new ChannelBytes(channel, message));
            }
            _peerServices.KnownNodes.Forget(key);
        }

        private IEnumerable<ReadOnlyMemory<byte>> SinkAsServer(ChannelBytes channelBytes, IResponder responder) {
            byte[] buffer = channelBytes.AllBytes;
            var command = (buffer.Length > 1) ? (char)buffer[1] : '\0';
            IEnumerable<byte> text = buffer.Skip(2);
            var channel = channelBytes.Channel;
            Console.WriteLine($"Received command '{command}' on channel {channel}");
            switch (command) {
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
                var key = StartAsKeyed(responder, text, channel);
                yield return FormatTextResponse($"Using keyed connection on {key}", isLast: false);
                break;

            default:
                yield return FormatTextResponse("?", isLast: true);
                break;
            }
        }

        private async Task SinkAsServerWithDelayedResponsesAsync(ChannelBytes channelBytes, IResponder responder) {
            var result = SinkAsServer(channelBytes, responder);
            var channel = channelBytes.Channel;
            if (result.Count() <= 1)
                Send(responder, channel, result.First());
            else {
                foreach (var r in result) {
                    Send(responder, channel, r);
                    await Task.Delay(2_000);
                }
            }
            Console.WriteLine($"Done processing on channel {channel}");
        }

        private string StartAsKeyed(IResponder responder, IEnumerable<byte> text, ulong channel) {
            var key = AsString(text);
            _peerServices.KnownNodes.Add(key, responder);
            _queueKeys.Enqueue((channel, key));
            return key;
        }
    }
}