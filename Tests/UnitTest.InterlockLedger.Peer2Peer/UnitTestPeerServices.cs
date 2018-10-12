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
        public void TestPeerListenerCreation() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
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
            var peerServices = new PeerServices(fakeLogger, fakeDiscoverer);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
        }
    }
}
