/******************************************************************************************************************************

Copyright (c) 2018-2020 InterlockLedger Network
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestListenerForProxying
    {
        [TestMethod]
        public void TestListenerForProxyingMinimally() {
            var fakeLogger = new FakeLogging();
            using var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeExternalSocket = new TestSocket(holdYourHorses: true, _tag, 1, 240, 128);
            var fakeInternalSocket = new TestSocket(holdYourHorses: true, _tag, 1, 241, 1);
            var fakeSink = new TestSink(_tag, 1, 242);
            var fakeNodeSink = new FakeNodeSink(_tag, 2000, 10, 40);
            using var referenceListener = new ListenerForPeer(fakeNodeSink, fakeDiscoverer, source, fakeLogger);
            var internalConnection = new ConnectionInitiatedByPeer("TLFPM", fakeNodeSink, fakeInternalSocket, fakeSink, source, fakeLogger);
            internalConnection.SetDefaultSink(fakeNodeSink);
            using var lfp = new TestListenerForProxying(fakeExternalSocket, referenceListener.ExternalAddress, referenceListener.ExternalAddress, 333, internalConnection, new SocketFactory(fakeLogger, 3), source, fakeLogger);
            lfp.Start();
            WaitForOthers(100);
            fakeExternalSocket.ReleaseYourHorses();
            var max = 5;
            while (AllBytes(fakeInternalSocket).Count() < 4 && max-- > 0)
                WaitForOthers(100);
            var allInternalBytes = AllBytes(fakeInternalSocket);
            Assert.IsNotNull(fakeLogger.LastLog);
            AssertHasSameItems<byte>(nameof(fakeInternalSocket.BytesSent), allInternalBytes, 240, 1);
            fakeInternalSocket.ReleaseYourHorses();
            max = 5;
            while (AllBytes(fakeExternalSocket).Count() < 4 && max-- > 0)
                WaitForOthers(100);
            AssertHasSameItems<byte>(nameof(fakeExternalSocket.BytesSent), AllBytes(fakeExternalSocket), 241, 128);
            fakeSink.Reset();
            fakeSink.SinkAsync(allInternalBytes.SkipLast(1).ToArray(), new TestChannel(allInternalBytes.Last())).Wait();
            max = 12;
            while (fakeSink.ChannelProcessed == 0ul && max-- > 0)
                WaitForOthers(100);
            WaitForOthers(100);
            AssertHasSameItems<byte>(nameof(fakeSink.BytesProcessed), fakeSink.BytesProcessed.ToArray(), 240);
            Assert.AreEqual((ulong)1, fakeSink.ChannelProcessed);
            AssertHasLogLine(fakeLogger, "Debug: Sinked Message '8A' from Channel ProxyingClient#1@128 using new pair to Proxied Channel 1. Sent: True");
            AssertHasLogLine(fakeLogger, "Debug: Responded with Message '8Q' from Channel TLFPM@1 to External Channel 128. Sent: True");
        }

        [TestMethod]
        public void TestListenerForProxyingWithSomeRealSockets() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeSink = new TestSink(_tag, 1, 242);
            var externalNodeSink = new ProxyNodeSink(_tag, 4000, fakeLogger, source);
            var internalNodeSink = new FakeNodeSink(_tag, 3000, 10, 40);
            using var referenceListener = new ListenerForPeer(externalNodeSink, fakeDiscoverer, source, fakeLogger);
            using var internalListener = new ListenerForPeer(internalNodeSink, fakeDiscoverer, source, fakeLogger);
            referenceListener.Start();
            internalListener.Start();
            using var internalConnection = new ConnectionToPeer("RequestProxying", internalNodeSink, referenceListener.ExternalAddress, referenceListener.ExternalPortNumber, source, fakeLogger, buildAliveMessage: null);
            internalConnection.AllocateChannel(internalNodeSink).SendAsync(ProxyNodeSink.ProxyRequest).Wait();
            int retries = 10;
            while (externalNodeSink.ListenerForProxying == null && retries-- > 0)
                WaitForOthers(100);
            var lfp = externalNodeSink.ListenerForProxying;
            Assert.IsNotNull(lfp, "Should have a ListenerForProxying by now");
            internalConnection.SetDefaultSink(fakeSink);
            lfp.Start();
            WaitForOthers(300);
            using var externalConnection = new ConnectionToPeer("ExternalMessage", internalNodeSink, lfp.ExternalAddress, lfp.ExternalPortNumber, source, fakeLogger, buildAliveMessage: null);
            externalConnection.AllocateChannel(externalNodeSink); // just to bump channel
            var outsideChannel = externalConnection.AllocateChannel(externalNodeSink);
            outsideChannel.SendAsync(new byte[] { _tag, 1, 2 }).Wait();
            retries = 10;
            while (fakeSink.ChannelProcessed == 0 && retries-- > 0)
                WaitForOthers(100);
            AssertHasSameItems<byte>(nameof(fakeSink.BytesProcessed), fakeSink.BytesProcessed.ToArray(), 2);
            Assert.AreEqual(1ul, fakeSink.ChannelProcessed);
            retries = 10;
            while (externalNodeSink.MessagesReceived.Count == 0 && retries-- > 0)
                WaitForOthers(100);
            AssertHasSameItems<byte>(nameof(externalNodeSink.MessagesReceived), externalNodeSink.MessagesReceived.SelectMany(l => l.ToArray()), 242);
            AssertHasLogLine(fakeLogger, "Debug: Sinked Message 'Ag' from Channel ProxyingClient#1@2 using new pair to Proxied Channel 1. Sent: True");
            AssertHasLogLine(fakeLogger, "Debug: Responded with Message 'DQHy' from Channel ListenerClient#1@1 to External Channel 2. Sent: True");
            lfp.Stop();
            retries = 10;
            while (lfp.Alive && retries-- > 0)
                WaitForOthers(100);
            WaitForOthers(300);
            Assert.IsTrue(referenceListener.Alive, "External listener should stay alive after lfp having stopped");
        }

        [TestMethod]
        public void TestListenerForProxyingWithSomeRealSocketsWithBrokenConnections() {
            //TODO use event to reinstate proxying
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeSink = new TestSink(_tag, 1, 242);
            var externalNodeSink = new ProxyNodeSink(_tag, 6000, fakeLogger, source);
            var internalNodeSink = new FakeNodeSink(_tag, 5000, 10, 40);
            using var referenceListener = new ListenerForPeer(externalNodeSink, fakeDiscoverer, source, fakeLogger);
            using var internalListener = new ListenerForPeer(internalNodeSink, fakeDiscoverer, source, fakeLogger);
            referenceListener.Start();
            internalListener.Start();
            using var internalConnection = new ConnectionToPeer("RequestProxying", internalNodeSink, referenceListener.ExternalAddress, referenceListener.ExternalPortNumber, source, fakeLogger, buildAliveMessage: null);
            internalConnection.AllocateChannel(internalNodeSink).SendAsync(ProxyNodeSink.ProxyRequest).Wait();
            int retries = 10;
            while (externalNodeSink.ListenerForProxying == null && retries-- > 0)
                WaitForOthers(100);
            var lfp = externalNodeSink.ListenerForProxying;
            Assert.IsNotNull(lfp, "Should have a ListenerForProxying by now");
            lfp.Start();
            WaitForOthers(300);
            internalConnection.SetDefaultSink(fakeSink);
            using var externalConnection = new ConnectionToPeer("ExternalMessage", internalNodeSink, lfp.ExternalAddress, lfp.ExternalPortNumber, source, fakeLogger, buildAliveMessage: null);
            externalConnection.AllocateChannel(externalNodeSink); // just to bump channel
            var outsideChannel = externalConnection.AllocateChannel(externalNodeSink);
            outsideChannel.SendAsync(new byte[] { _tag, 1, 2 }).Wait();
            while (fakeSink.ChannelProcessed == 0)
                WaitForOthers(100);
            AssertHasSameItems<byte>(nameof(fakeSink.BytesProcessed), fakeSink.BytesProcessed.ToArray(), 2);
            Assert.AreEqual(1ul, fakeSink.ChannelProcessed);
            while (externalNodeSink.MessagesReceived.Count == 0)
                WaitForOthers(100);
            AssertHasSameItems<byte>(nameof(externalNodeSink.MessagesReceived), externalNodeSink.MessagesReceived.SelectMany(l => l.ToArray()), 242);
            AssertHasLogLine(fakeLogger, "Debug: Sinked Message 'Ag' from Channel ProxyingClient#1@2 using new pair to Proxied Channel 1. Sent: True");
            AssertHasLogLine(fakeLogger, "Debug: Responded with Message 'DQHy' from Channel ListenerClient#1@1 to External Channel 2. Sent: True");
            lfp.Stop();
            while (lfp.Alive)
                WaitForOthers(100);
            WaitForOthers(300);
            Assert.IsTrue(referenceListener.Alive, "External listener should stay alive after lfp having stopped");
        }

        private const byte _tag = 13;

        private static IEnumerable<byte> AllBytes(TestSocket fakeProxiedSocket)
            => ToBytes(fakeProxiedSocket.BytesSent);

        private class ProxyNodeSink : FakeNodeSink
        {
            public static readonly byte[] ProxyRequest = new byte[] { _tag, 2, 128, 129 };

            public ProxyNodeSink(ulong messageTag, ushort port, FakeLogging fakeLogger, CancellationTokenSource source)
                : base(messageTag, port, 10, 40) {
                _fakeLogger = fakeLogger ?? throw new ArgumentNullException(nameof(fakeLogger));
                _source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public ListenerForProxying ListenerForProxying { get; private set; }

            public override Task<Success> SinkAsync(ReadOnlyMemory<byte> messageBytes, IActiveChannel channel) {
                if (messageBytes.ToArray().SequenceEqual(ProxyRequest.Skip(2))) {
                    ListenerForProxying = new ListenerForProxying(HostAtAddress, HostAtAddress, (ushort)(HostAtPortNumber - 1), channel.Connection, new SocketFactory(_fakeLogger, 3), _source, _fakeLogger);
                    return Task.FromResult(Success.Next);
                }
                return base.SinkAsync(messageBytes, channel);
            }

            private readonly FakeLogging _fakeLogger;
            private readonly CancellationTokenSource _source;
        }

        private class TestChannel : IActiveChannel
        {
            public TestChannel(ulong channel) => Channel = channel;

            public bool Active { get; } = true;
            public ulong Channel { get; }
            public bool Connected { get; } = true;
            public IConnection Connection { get; }
            public string Id { get; }

            public Task<bool> SendAsync(ReadOnlyMemory<byte> messageBytes) => Task.FromResult(true);

            public Task<Success> SinkAsync(ReadOnlyMemory<byte> messageBytes) => Task.FromResult(Success.Next);

            public void Stop() {
            }
        }
    }
}
