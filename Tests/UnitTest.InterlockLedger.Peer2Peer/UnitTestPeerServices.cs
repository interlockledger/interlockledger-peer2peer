/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
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
            INodeSink fakeNodeSink = new FakeNodeSync();
            var source = new CancellationTokenSource();
            var peerClient = peerServices.GetClient(fakeNodeSink.MessageTag, "test", "localhost", 80, source);
            Assert.IsNotNull(peerClient);
            Assert.IsNull(fakeLogger.LastLog);
            var peerClient2 = peerServices.GetClient(fakeNodeSink.MessageTag, "test2", source);
            Assert.IsNull(peerClient2);
            Assert.IsNull(fakeLogger.LastLog);
            peerServices.AddKnownNode("test3", "localhost", 80);
            var peerClient3 = peerServices.GetClient(fakeNodeSink.MessageTag, "test3", source);
            Assert.IsNotNull(peerClient3);
            Assert.IsNull(fakeLogger.LastLog);
        }

        [TestMethod]
        public void TestPeerListenerCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            IPeerServices peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
            INodeSink fakeNodeSink = new FakeNodeSync();
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
            INodeSink fakeNodeSink = new FakeNodeSync();
            var source = new CancellationTokenSource();
            var peerListener = peerServices.CreateFor(fakeNodeSink, source);
            Assert.IsNull(peerListener);
            Assert.IsNull(fakeLogger.LastLog);
            var peerClient = peerServices.GetClient(fakeNodeSink.MessageTag, "test", "localhost", 80, source);
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