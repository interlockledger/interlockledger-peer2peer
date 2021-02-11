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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestPeerServices
    {
        [TestMethod]
        public void TestPeerClientCreation() {
            var fakeLogger = new FakeLogging();
            var socketFactory = new SocketFactory(fakeLogger, 10);
            using var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            using IPeerServices peerServices = new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, fakeDiscoverer, socketFactory, 10, 40, buildAliveMessage: null);
            peerServices.WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            var peerClient = peerServices.GetClient("localhost", 80);
            Assert.IsNotNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            var knownNodes = peerServices.KnownNodes;
            var peerClient2 = knownNodes.GetClient("test2");
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.AreEqual(false, knownNodes.IsKnown("test2"));
            knownNodes.Add("test3", "localhost", 80);
            var peerClient3 = knownNodes.GetClient("test3");
            Assert.AreEqual(true, knownNodes.IsKnown("test3"));
            Assert.IsNotNull(peerClient3);
            Assert.IsNull(fakeLogger.LastLog);
            knownNodes.Add("test4", peerClient3);
            Assert.AreEqual(true, knownNodes.IsKnown("test4"));
            Assert.AreEqual(peerClient3, knownNodes.GetClient("test4"));
            Assert.AreEqual(peerClient3, peerServices.GetClient("test4"));
        }

        [TestMethod]
        public void TestPeerListenerCreation() {
            var fakeLogger = new FakeLogging();
            var socketFactory = new SocketFactory(fakeLogger, 10);
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            using IPeerServices peerServices = new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, fakeDiscoverer, socketFactory, 10, 40, buildAliveMessage: null);
            peerServices.WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink(_messageTag, 2002, 10, 40);
            using var peerListener = peerServices.CreateListenerFor(fakeNodeSink);
            Assert.IsNotNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestPeerServicesCreation() {
            var fakeLogger = new FakeLogging();
            var socketFactory = new SocketFactory(fakeLogger, 10);
            var fakeDiscoverer = new FakeDiscoverer();
            using INodeSink fakeNodeSink = new FakeNodeSink(_messageTag, 2003, 10, 40);
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(_messageTag, "UnitTest", "unit", 4096, null, fakeDiscoverer, socketFactory, 10, 40, buildAliveMessage: null));
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, null, socketFactory, 10, 40, buildAliveMessage: null));
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, fakeDiscoverer, null, 10, 40, buildAliveMessage: null));
            IPeerServices peerServices = new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, fakeDiscoverer, socketFactory, 10, 40, buildAliveMessage: null);
            Assert.IsNotNull(peerServices);
            Assert.IsNotNull(peerServices.ProxyingServices);
            Assert.IsNotNull(peerServices.KnownNodes);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.IsNull(peerServices.GetClient("localhost", 80));
            Assert.IsNotNull(fakeLogger.LastLog);
            Assert.ThrowsException<InvalidOperationException>(() => peerServices.CreateListenerFor(fakeNodeSink));
            Assert.ThrowsException<ArgumentNullException>(() => peerServices.WithCancellationTokenSource(null));
            peerServices.WithCancellationTokenSource(new CancellationTokenSource());
            Assert.IsNotNull(peerServices.Source);
            var client = peerServices.GetClient("localhost", 80);
            Assert.IsNotNull(client);
        }

        [TestMethod]
        public void TestPeerServicesDisposal() {
            var fakeLogger = new FakeLogging();
            var socketFactory = new SocketFactory(fakeLogger, 10);
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            using IPeerServices peerServices = new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, fakeDiscoverer, socketFactory, 10, 40, buildAliveMessage: null);
            peerServices.WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink(_messageTag, 2004, 10, 40);
            peerServices.KnownNodes.Add("nodeToForget", "localhost", 80);
            Assert.IsTrue(peerServices.KnownNodes.IsKnown("nodeToForget"));
            peerServices.Dispose();
            Assert.IsFalse(peerServices.KnownNodes.IsKnown("nodeToForget"));
            var peerListener = peerServices.CreateListenerFor(fakeNodeSink);
            Assert.IsNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
            var peerClient = peerServices.GetClient("localhost", 80);
            Assert.IsNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            peerServices.KnownNodes.Add("test2", "localhost", 80);
            Assert.IsFalse(peerServices.KnownNodes.IsKnown("test2"));
            var peerClient2 = peerServices.KnownNodes.GetClient("test2");
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestProxyListenerCreation() {
            var fakeLogger = new FakeLogging();
            var socketFactory = new SocketFactory(fakeLogger, 10);
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            using IPeerServices peerServices = new PeerServices(_messageTag, "UnitTest", "unit", 4096, fakeLogger, fakeDiscoverer, socketFactory, 10, 40, null);
            peerServices.WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink(_messageTag, 8002, 10, 40);
            IConnection connection = new ConnectionToPeer("Test", fakeNodeSink, "localhost", 8003, source, fakeLogger, null);
            using var peerListener = peerServices.ProxyingServices.CreateListenerForProxying("rafael.interlockledger.network", "localhost", 9000, connection);
            Assert.IsNotNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.AreEqual("rafael.interlockledger.network", peerListener.ExternalAddress);
        }

        private const ulong _messageTag = '?';
    }
}