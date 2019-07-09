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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestListenerForProxying
    {
        [TestMethod]
        public void TestListenerForProxyingMinimally() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeExternalSocket = new TestSocket(holdYourHorses: true, _tag, 1, 240, 128);
            var fakeInternalSocket = new TestSocket(holdYourHorses: true, _tag, 1, 241, 1);
            var fakeSink = new TestSink(_tag, 1, 242);
            var fakeNodeSink = new FakeNodeSink(_tag, 2000);
            using (var referenceListener = new ListenerForPeer(fakeNodeSink, fakeDiscoverer, source, fakeLogger)) {
                var internalConnection = new ConnectionInitiatedByPeer("TLFPM", fakeNodeSink, fakeInternalSocket, fakeSink, source, fakeLogger);
                internalConnection.SetDefaultSink(fakeNodeSink);
                var lfp = new TestListenerForProxying(fakeExternalSocket, referenceListener.ExternalAddress, 333, internalConnection, new SocketFactory(fakeLogger, 3), source, fakeLogger);
                lfp.Start();
                WaitForOthers(100);
                fakeExternalSocket.ReleaseYourHorses();
                var max = 5;
                while (AllBytes(fakeInternalSocket).Count() < 4 && max-- > 0)
                    WaitForOthers(100);
                IEnumerable<byte> allInternalBytes = AllBytes(fakeInternalSocket);
                Assert.IsNotNull(fakeLogger.LastLog);
                AssertHasSameItems<byte>(nameof(fakeInternalSocket.BytesSent), allInternalBytes, _tag, 1, 240, 1);
                fakeInternalSocket.ReleaseYourHorses();
                max = 5;
                while (AllBytes(fakeExternalSocket).Count() < 4 && max-- > 0)
                    WaitForOthers(100);
                AssertHasSameItems<byte>(nameof(fakeExternalSocket.BytesSent), AllBytes(fakeExternalSocket), _tag, 1, 241, 128);
                fakeSink.SinkAsync(allInternalBytes.SkipLast(1), new TestChannel(allInternalBytes.Last())).Wait();
                AssertHasSameItems<byte>(nameof(fakeSink.bytesProcessed), fakeSink.bytesProcessed, _tag, 1, 240);
                Assert.AreEqual((ulong)1, fakeSink.channelProcessed);
            }
        }

        [TestMethod]
        public void TestListenerForProxyingWithSomeRealSockets() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeSink = new TestSink(_tag, 1, 242);
            var externalNodeSink = new ProxyNodeSink(_tag, 4000, fakeLogger, source);
            var internalNodeSink = new FakeNodeSink(_tag, 3000);
            using (var referenceListener = new ListenerForPeer(externalNodeSink, fakeDiscoverer, source, fakeLogger)) {
                using (var internalListener = new ListenerForPeer(internalNodeSink, fakeDiscoverer, source, fakeLogger)) {
                    referenceListener.Start();
                    internalListener.Start();
                    var internalConnection = new ConnectionToPeer("RequestProxying", internalNodeSink, referenceListener.ExternalAddress, referenceListener.ExternalPortNumber, source, fakeLogger);
                    internalConnection.AllocateChannel(internalNodeSink).Send(ProxyNodeSink.ProxyRequest);
                    while (externalNodeSink.ListenerForProxying == null)
                        WaitForOthers(100);
                    var lfp = externalNodeSink.ListenerForProxying;
                    lfp.Start();
                    WaitForOthers(300);
                    internalConnection.SetDefaultSink(fakeSink);
                    var externalConnection = new ConnectionToPeer("ExternalMessage", internalNodeSink, lfp.ExternalAddress, lfp.ExternalPortNumber, source, fakeLogger);
                    IActiveChannel outsideChannel = externalConnection.AllocateChannel(externalNodeSink);
                    outsideChannel.Send(new byte[] { _tag, 1, 2 });
                    while (fakeSink.channelProcessed == 0)
                        WaitForOthers(100);
                    AssertHasSameItems<byte>(nameof(fakeSink.bytesProcessed), fakeSink.bytesProcessed, 2);
                    Assert.AreEqual(outsideChannel.Channel, fakeSink.channelProcessed);
                    while (externalNodeSink.MessagesReceived.Count == 0)
                        WaitForOthers(100);
                    AssertHasSameItems<byte>(nameof(externalNodeSink.MessagesReceived), externalNodeSink.MessagesReceived.SelectMany(l => l), 242);
                }
            }
        }

        private const byte _tag = 13;

        private static IEnumerable<byte> AllBytes(TestSocket fakeProxiedSocket)
            => fakeProxiedSocket.BytesSent.SelectMany(a => a);

        private class ProxyNodeSink : FakeNodeSink
        {
            public static readonly byte[] ProxyRequest = new byte[] { _tag, 2, 128, 129 };

            public ProxyNodeSink(ulong messageTag, ushort port, FakeLogging fakeLogger, CancellationTokenSource source) : base(messageTag, port) {
                _fakeLogger = fakeLogger ?? throw new ArgumentNullException(nameof(fakeLogger));
                _source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public ListenerForProxying ListenerForProxying { get; private set; }

            public override Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel) {
                if (message.SequenceEqual(ProxyRequest.Skip(2))) {
                    ListenerForProxying = new ListenerForProxying(HostAtAddress, (ushort)(HostAtPortNumber - 1), channel.Connection, new SocketFactory(_fakeLogger, 3), _source, _fakeLogger);
                    return Task.FromResult(Success.Next);
                }
                return base.SinkAsync(message, channel);
            }

            private readonly FakeLogging _fakeLogger;
            private readonly CancellationTokenSource _source;
        }

        private class TestChannel : IActiveChannel
        {
            public TestChannel(ulong channel) => Channel = channel;

            public bool Active { get; } = true;
            public ulong Channel { get; }
            public IConnection Connection { get; }
            public string Id { get; }

            public bool Send(IEnumerable<byte> message) => true;

            public Task<Success> SinkAsync(IEnumerable<byte> message) => Task.FromResult(Success.Next);
        }
    }
}