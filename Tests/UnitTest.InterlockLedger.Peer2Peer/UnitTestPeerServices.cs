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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace UnitTest.InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestPeerServices
    {
        [TestMethod]
        public void TestPeerClientCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer).WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink();
            var peerClient = peerServices.GetClient(fakeNodeSink.MessageTag, "localhost", 80, 512);
            Assert.IsNotNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            IKnownNodesServices knownNodes = peerServices.KnownNodes;
            var peerClient2 = knownNodes.GetClient("test2", 512);
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.AreEqual(false, knownNodes.IsKnown("test2"));
            knownNodes.Add("test3", fakeNodeSink.MessageTag, "localhost", 80);
            var peerClient3 = knownNodes.GetClient("test3", 512);
            Assert.AreEqual(true, knownNodes.IsKnown("test3"));
            Assert.IsNotNull(peerClient3);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestPeerListenerCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer).WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink();
            var peerListener = peerServices.CreateListenerFor(fakeNodeSink);
            Assert.IsNotNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestPeerServicesCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            INodeSink fakeNodeSink = new FakeNodeSink();
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(null, fakeDiscoverer));
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(fakeLogger, null));
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.ThrowsException<InvalidOperationException>(() => peerServices.GetClient(13, "localhost", 80, 512));
            Assert.ThrowsException<InvalidOperationException>(() => peerServices.CreateListenerFor(fakeNodeSink));
            Assert.ThrowsException<ArgumentNullException>(() => peerServices.WithCancellationTokenSource(null));
            peerServices.WithCancellationTokenSource(new CancellationTokenSource());
            Assert.IsNotNull(peerServices.Source);
        }

        [TestMethod]
        public void TestPeerServicesDisposal() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(null, fakeDiscoverer));
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(fakeLogger, null));
            var source = new CancellationTokenSource();
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer).WithCancellationTokenSource(source);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink();
            peerServices.KnownNodes.Add("nodeToForget", fakeNodeSink.MessageTag, "localhost", 80);
            Assert.IsTrue(peerServices.KnownNodes.IsKnown("nodeToForget"));
            peerServices.Dispose();
            Assert.IsFalse(peerServices.KnownNodes.IsKnown("nodeToForget"));
            var peerListener = peerServices.CreateListenerFor(fakeNodeSink);
            Assert.IsNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
            var peerClient = peerServices.GetClient(fakeNodeSink.MessageTag, "localhost", 80, 512);
            Assert.IsNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            peerServices.KnownNodes.Add("test2", fakeNodeSink.MessageTag, "localhost", 80);
            Assert.IsFalse(peerServices.KnownNodes.IsKnown("test2"));
            var peerClient2 = peerServices.KnownNodes.GetClient("test2", 512);
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
        }
    }
}