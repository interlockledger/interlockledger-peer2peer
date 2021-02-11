/******************************************************************************************************************************
 
Copyright (c) 2018-2020 InterlockLedger Network
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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static InterlockLedger.Peer2Peer.TestHelpers;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestPipeline
    {
        [TestMethod]
        public void TestPipelineMinimally() {
            bool stopped = false;
            string stoppedId = null;
            ReadOnlyMemory<byte> bytesProcessed = new();
            ulong channelProcessed = 0;
            var fakeLogger = new FakeLogging();
            using var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeSocket = new TestSocket(13, 1, 128, 2);
            using var fakeClient = new TestClient("FakeTestClient");

            async Task<Success> processor(NetworkMessageSlice channelBytes) {
                bytesProcessed = channelBytes.AllBytes;
                channelProcessed = channelBytes.Channel;
                var activeChannel = fakeClient.GetChannel(channelProcessed);
                await activeChannel.SendAsync(bytesProcessed);
                await Task.Delay(1).ConfigureAwait(false);
                return Success.Exit;
            }
            void stopProcessor() {
                stopped = true;
                fakeClient.OnPipelineStopped();
            }
            fakeClient.ConnectionStopped += (i) => stoppedId = i.Id;
            var pipeline = new Pipeline(fakeSocket, source, 13, 4096, processor, stopProcessor, fakeLogger, 10);
            Assert.IsNotNull(pipeline);
            fakeClient.Pipeline = pipeline;
            Assert.IsNull(fakeLogger.LastLog);
            pipeline.ListenAsync().RunOnThread("UnitTestPipeline");
            while (!(pipeline.NothingToSend && fakeSocket.Available == 0))
                Thread.Sleep(1);
            pipeline.Stop();
            while (!pipeline.Stopped)
                Thread.Sleep(1);
            Assert.IsFalse(bytesProcessed.IsEmpty);
            Assert.IsNotNull(fakeLogger.LastLog);
            Assert.AreEqual(2ul, channelProcessed);
            AssertHasSameItems<byte>(nameof(bytesProcessed), bytesProcessed.ToArray(), 128);
            Assert.IsTrue(stopped, "StopProcessor should have been called");
            Assert.IsNotNull(stoppedId);
            Assert.AreEqual(fakeClient.Id, stoppedId);
            Assert.IsNotNull(fakeSocket.BytesSent);
            AssertHasSameItems<byte>(nameof(fakeSocket.BytesSent), ToBytes(fakeSocket.BytesSent), 128, 2);
            Assert.IsTrue(fakeSocket.Disposed, "Socket should have been disposed");
        }
    }
}