// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2023 InterlockLedger Network
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met
//
// * Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// * Neither the name of the copyright holder nor the names of its
//   contributors may be used to endorse or promote products derived from
//   this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES, LOSS OF USE, DATA, OR PROFITS, OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// ******************************************************************************************************************************

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Buffers;
using System.Threading;

using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestConnectionBase
    {
        [TestMethod]
        public void TestConnectionMinimally() {
            var connection = Setup(10, out var fakeLogger, out var fakeSocket, out var fakeSink);
            Assert.IsNotNull(connection);
            Thread.Sleep(400);
            Assert.IsFalse(fakeSink.BytesProcessed.IsEmpty);
            Assert.IsNotNull(fakeLogger.LastLog);
            AssertHasSameItems<byte>(nameof(fakeSink.BytesProcessed), fakeSink.BytesProcessed.ToArray(), 128);
            Assert.IsFalse(fakeSocket.BytesSent.IsEmpty);
            AssertHasSameItems<byte>(nameof(fakeSocket.BytesSent), fakeSocket.BytesSent.ToArray(), 13, 1, 129, 2);
            connection.AllocateChannel(fakeSink);
            Assert.AreEqual(1, connection.LastChannelUsed);
            Assert.AreEqual(2, connection.NumberOfActiveChannels);
            Assert.IsNotNull(connection.GetChannel(1));
            var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() => connection.GetChannel(10));
            Assert.AreEqual("channel", ex.ParamName);
            Assert.AreEqual(string.Format(ConnectionBase.ExceptionChannelNotFoundFormat, 10) + " (Parameter 'channel')", ex.Message);
        }

        [TestMethod, Timeout(120_000)]
        public void TestConnectionTimeout() {
            var connection = Setup(inactivityTimeoutInMinutes: 1, out var fakeLogger, out var fakeSocket, out var fakeSink);
            Assert.IsNotNull(connection);
            string stoppedId = null;
            connection.ConnectionStopped += (n) => stoppedId = n.Id;
            Assert.IsFalse(connection.Disposed);
            Assert.IsNull(stoppedId);
            Thread.Sleep(60_500);
            Assert.IsNotNull(stoppedId);
            Assert.IsTrue(connection.Disposed);
        }

        [TestMethod, Timeout(30_000)]
        public void TestConnectionNonTimeout() {
            var connection = Setup(inactivityTimeoutInMinutes: 0, out var fakeLogger, out var fakeSocket, out var fakeSink);
            Assert.IsNotNull(connection);
            string stoppedId = null;
            connection.ConnectionStopped += (n) => stoppedId = n.Id;
            Assert.IsFalse(connection.Disposed);
            Assert.IsNull(stoppedId);
            Thread.Sleep(10_500);
            Assert.IsNull(stoppedId);
            Assert.IsFalse(connection.Disposed);
        }

        private static TestConnection Setup(int inactivityTimeoutInMinutes, out FakeLogging fakeLogger, out TestSocket fakeSocket, out TestSink fakeSink) {
            fakeLogger = new FakeLogging();
            var source = new CancellationTokenSource();
            fakeSocket = new TestSocket(13, 1, 128, 2);
            fakeSink = new TestSink(13, 1, 129);
            var fakeConfig = new FakeConfig(13, "UnitTest", "unit", 4096, inactivityTimeoutInMinutes, 4);
            return new TestConnection(fakeSocket, fakeSink, "TestConnection", fakeConfig, source, fakeLogger, null);
        }
    }
}