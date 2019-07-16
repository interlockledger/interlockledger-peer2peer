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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal class DemoClient : DemoBaseSink
    {
        public DemoClient() : base("Client") {
        }

        public bool DoneReceiving { get; set; } = false;

        public string Prompt => @"Command (
    x to exit,
    w to get who is answering,
    e... to echo ...,
    3... to echo ... 3 times,
    4... to echo ... 4 times from stored responder): ";

        public override async Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel activeChannel)
            => Received(await Process(message, activeChannel.Channel));

        protected override void Run(IPeerServices peerServices) {
            using (var client = peerServices.GetClient("localhost", 8080)) {
                var liveness = new LivenessListener(client);
                while (!_source.IsCancellationRequested) {
                    Console.WriteLine();
                    Console.Write(Prompt);
                    while (!Console.KeyAvailable) {
                        if (!liveness.Alive) {
                            Console.WriteLine();
                            Console.WriteLine();
                            Console.WriteLine("Lost connection with server! Abandoning...");
                            Console.WriteLine();
                            return;
                        }
                    }
                    var command = Console.ReadLine();
                    if (command == null || command.FirstOrDefault() == 'x')
                        break;
                    var channel = client.AllocateChannel(this);
                    channel.Send(ToMessage(AsUTF8Bytes(command), isLast: true).AllBytes);
                }
            }
        }

        private static async Task<Success> Process(IEnumerable<byte> message, ulong channel) {
            await Task.Delay(1);
            if (message.Any()) {
                var response = Encoding.UTF8.GetString(message.Skip(1).ToArray());
                Console.WriteLine($@"[{channel}] {response}");
                return response[0] == 0 ? Success.Exit : Success.Next;
            }
            return Success.Exit;
        }

        private Success Received(Success success) {
            DoneReceiving = success == Success.Exit;
            return success;
        }

        private class LivenessListener : IChannelSink
        {
            public LivenessListener(IConnection client) => Start(client.AllocateChannel(this));

            public bool Alive { get; private set; } = true;

            public Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel) => Task.FromResult(Success.Next);

            private void Start(IActiveChannel channel)
                => Task.Run(() => {
                    try {
                        while (channel.Connected) {
                            channel.Send(ToMessage(AsUTF8Bytes("l"), isLast: true).AllBytes);
                            Task.Delay(1000).Wait();
                        }
                    } catch {
                    } finally {
                        Alive = false;
                    }
                }).RunOnThread("Liveness");
        }
    }
}