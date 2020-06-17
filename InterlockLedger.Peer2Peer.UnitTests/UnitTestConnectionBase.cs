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

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestConnectionBase
    {
        [TestMethod]
        public void TestConnectionMinimally() {
            var fakeLogger = new FakeLogging();
            var source = new CancellationTokenSource();
            var fakeSocket = new TestSocket(13, 1, 128, 2);
            var fakeSink = new TestSink(13, 1, 129);
            var fakeConfig = new FakeConfig(13, "UnitTest", "unit", 4096);
            var connection = new TestConnection(fakeSocket, fakeSink, "TestConnection", fakeConfig, source, fakeLogger);
            Assert.IsNotNull(connection);
            Thread.Sleep(400);
            Assert.IsNotNull(fakeSink.BytesProcessed);
            Assert.IsNotNull(fakeLogger.LastLog);
            AssertHasSameItems<byte>(nameof(fakeSink.BytesProcessed), fakeSink.BytesProcessed, 128);
            Assert.IsNotNull(fakeSocket.BytesSent);
            AssertHasSameItems<byte>(nameof(fakeSocket.BytesSent), ToBytes(fakeSocket.BytesSent), 13, 1, 129, 2);
            connection.AllocateChannel(fakeSink);
            Assert.AreEqual(1, connection.LastChannelUsed);
            Assert.AreEqual(2, connection.NumberOfActiveChannels);
            Assert.IsNotNull(connection.GetChannel(1));
            var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() => connection.GetChannel(10));
            Assert.AreEqual("channel", ex.ParamName);
            Assert.AreEqual(string.Format(ConnectionBase.ExceptionChannelNotFoundFormat, 10) + " (Parameter 'channel')", ex.Message);
        }
    }
}