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
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink();
            var source = new CancellationTokenSource();
            var peerClient = peerServices.GetClient(fakeNodeSink.MessageTag, "localhost", 80, source);
            Assert.IsNotNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.AreEqual(0, peerClient.SocketHashCode);
            var peerClient2 = peerServices.GetClient(fakeNodeSink.MessageTag, "test2", source);
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
            peerServices.AddKnownNode("test3", "localhost", 80);
            var peerClient3 = peerServices.GetClient(fakeNodeSink.MessageTag, "test3", source);
            Assert.IsNotNull(peerClient3);
            Assert.IsNull(fakeLogger.LastLog);
            Assert.AreEqual(0, peerClient3.SocketHashCode);
            Assert.ThrowsException<AggregateException>(() => peerClient3.Reconnect());
        }

        [TestMethod]
        public void TestPeerListenerCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSink();
            var peerListener = peerServices.CreateFor(fakeNodeSink, new CancellationTokenSource());
            Assert.IsNotNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestPeerServicesCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(null, fakeDiscoverer));
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(fakeLogger, null));
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestPeerServicesDisposal() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(null, fakeDiscoverer));
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(fakeLogger, null));
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            peerServices.AddKnownNode("nodeToForget", "localhost", 80);
            Assert.IsTrue(peerServices.IsNodeKnown("nodeToForget"));
            peerServices.Dispose();
            Assert.IsFalse(peerServices.IsNodeKnown("nodeToForget"));
            INodeSink fakeNodeSink = new FakeNodeSink();
            var source = new CancellationTokenSource();
            var peerListener = peerServices.CreateFor(fakeNodeSink, source);
            Assert.IsNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
            var peerClient = peerServices.GetClient(fakeNodeSink.MessageTag, "localhost", 80, source);
            Assert.IsNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            peerServices.AddKnownNode("test2", "localhost", 80);
            Assert.IsFalse(peerServices.IsNodeKnown("test2"));
            var peerClient2 = peerServices.GetClient(fakeNodeSink.MessageTag, "test2", source);
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
        }
    }
}