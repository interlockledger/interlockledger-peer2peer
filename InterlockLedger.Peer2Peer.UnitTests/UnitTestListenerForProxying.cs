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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestListenerForProxying
    {
        [TestMethod]
        public void TestListenerForProxyingMinimally() {
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeRedirectedSocket = new TestSocket(holdYourHorses: true, _tag, 1, 240, 2);
            var fakeProxiedSocket = new TestSocket(holdYourHorses: true, _tag, 1, 241, 1);
            var fakeSink = new TestSink(_tag, 1, 242);
            var fakeNodeSink = new FakeNodeSink(_tag, 2000);
            using (var referenceListener = new ListenerForPeer(fakeNodeSink, fakeDiscoverer, source, fakeLogger)) {
                var connectionProxied = new ConnectionInitiatedByPeer("TLFPM", fakeNodeSink, fakeProxiedSocket, fakeSink, source, fakeLogger);
                var lfp = new TestListenerForProxying(fakeRedirectedSocket, referenceListener.ExternalAddress, 333, connectionProxied, new SocketFactory(fakeLogger, 3), source, fakeLogger);
                lfp.Start();
                WaitForOthers(100);
                fakeRedirectedSocket.ReleaseYourHorses();
                var max = 5;
                while (AllBytes(fakeProxiedSocket).Count() < 4 && max-- > 0)
                    WaitForOthers(100);
                Assert.IsNotNull(fakeLogger.LastLog);
                AssertHasSameItems(nameof(fakeProxiedSocket.BytesSent), AllBytes(fakeProxiedSocket), _tag, (byte)1, (byte)240, (byte)1);
                fakeProxiedSocket.ReleaseYourHorses();
                max = 5;
                while (AllBytes(fakeRedirectedSocket).Count() < 4 && max-- > 0)
                    WaitForOthers(100);
                Assert.AreEqual(1, fakeRedirectedSocket.BytesSent.Count);
                AssertHasSameItems<byte>(nameof(fakeRedirectedSocket.BytesSent), AllBytes(fakeRedirectedSocket), _tag, 1, 241, 2);
            }
        }

        private const byte _tag = 13;

        private static IEnumerable<byte> AllBytes(TestSocket fakeProxiedSocket)
            => fakeProxiedSocket.BytesSent.SelectMany(a => a);
    }
}