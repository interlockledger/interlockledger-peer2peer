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

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    public class TestListenerForPeer : ListenerForPeer
    {
        public TestListenerForPeer(TestSocket testSocket, INodeSink nodeSink, IExternalAccessDiscoverer discoverer, CancellationTokenSource source, ILogger logger) : base(nodeSink, discoverer, source, logger)
            => _testSocket = testSocket;

        protected override Func<Socket, Task<ISocket>> AcceptSocket => async (s) => {
            var socket = _testSocket;
            _testSocket = null;
            return socket ?? await AwaitCancelation();
        };

        private TestSocket _testSocket;

        private async Task<ISocket> AwaitCancelation() {
            while (!_source.IsCancellationRequested)
                await Task.Delay(100);
            return null;
        }
    }

    [TestClass]
    public class UnitTestListenerForProxying
    {
        [TestMethod]
        public void TestListenerForProxyingMinimally() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeSocket = new TestSocket(source, 13, 1, 128, 2);
            var fakeRedirectedSocket = new TestSocket(source, holdYourHorses: true, 13, 1, 240, 2);
            var fakeSink = new TestSink();
            var connection = new TestConnection(fakeSocket, fakeSink, "TestConnection", 13, source, fakeLogger, 4096);
            Assert.IsNotNull(connection);
            var fakeNodeSink = new FakeNodeSink();
            var mainListener = new TestListenerForPeer(fakeRedirectedSocket, fakeNodeSink, fakeDiscoverer, source, fakeLogger);
            mainListener.Start();
            var connectionProxied = new ConnectionInitiatedByPeer("TLFPM", 13, fakeSocket, fakeSink, source, fakeLogger, 16);
            var lfp = new ListenerForProxying(mainListener, 333, connectionProxied, new SocketFactory(fakeLogger, 3), source, fakeLogger);
            lfp.Start();
            Thread.Sleep(200);
            fakeRedirectedSocket.ReleaseYourHorses();
            Thread.Sleep(200);
            Assert.IsNotNull(fakeSink.bytesProcessed);
            Assert.IsNotNull(fakeLogger.LastLog);
            AssertHasSameItems<byte>(nameof(fakeSink.bytesProcessed), fakeSink.bytesProcessed, 128);
            Assert.IsNotNull(fakeSocket.BytesSent);
            AssertHasSameItems<byte>(nameof(fakeSocket.BytesSent), ToBytes(fakeSocket.BytesSent), 13, 1, 128, 2);
            Assert.IsNotNull(fakeRedirectedSocket.BytesSent);
            AssertHasSameItems<byte>(nameof(fakeRedirectedSocket.BytesSent), ToBytes(fakeRedirectedSocket.BytesSent), 13, 1, 240, 2);
            connection.AllocateChannel(fakeSink);
            Assert.AreEqual(1, connection.LastChannelUsed);
            Assert.AreEqual(1, connection.NumberOfActiveChannels);
            Assert.IsNotNull(connection.GetChannel(1));
        }
    }
}