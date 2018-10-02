/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using InterlockLedger.Peer2Peer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace UnitTest.InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestPeerServices
    {
        [TestMethod]
        public void TestCreation() {
            Assert.ThrowsException<ArgumentNullException>(() => new PeerServices(null));
            var fakeLogger = new FakeLogging();
            var peerServices = new PeerServices(fakeLogger);
            Assert.IsNotNull(peerServices);
            Assert.IsNull(fakeLogger.LastLog);
        }
    }
}