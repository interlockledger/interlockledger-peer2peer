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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static UnitTest.InterlockLedger.Peer2Peer.TestHelpers;

namespace UnitTest.InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestPipeline
    {
        [TestMethod]
        public void TestPipelineMinimally() {
            bool stopped = false;
            byte[] bytesProcessed = null;
            ulong channelProcessed = 0;
            var fakeLogger = new FakeLogging();
            var fakeDiscoverer = new FakeDiscoverer();
            var source = new CancellationTokenSource();
            var fakeSocket = new TestSocket(source, 13, 1, 128, 2);
            var fakeClient = new TestClient("FakeTestClient");
            async Task<Success> processor(NetworkMessageSlice channelBytes) {
                bytesProcessed = channelBytes.AllBytes;
                channelProcessed = channelBytes.Channel;
                var activeChannel = fakeClient.GetChannel(channelProcessed);
                activeChannel.Send(new byte[] { 13, 1, 128 });
                await Task.Delay(100);
                fakeClient.Stop();
                return Success.Exit;
            }
            void stopProcessor() => stopped = true;
            var pipeline = new Pipeline(fakeSocket, source, 13, 4096, processor, stopProcessor, fakeLogger);
            Assert.IsNotNull(pipeline);
            fakeClient.Pipeline = pipeline;
            Assert.IsNull(fakeLogger.LastLog);
            pipeline.ListenAsync().Wait();
            Assert.IsNotNull(bytesProcessed);
            Assert.IsNotNull(fakeLogger.LastLog);
            Assert.AreEqual(2ul, channelProcessed);
            AssertHasSameItems<byte>(nameof(bytesProcessed), bytesProcessed, 128);
            Assert.IsNotNull(fakeSocket.BytesSent);
            AssertHasSameItems<byte>(nameof(fakeSocket.BytesSent), ToBytes(fakeSocket.BytesSent), 13, 1, 128, 2);
            Assert.IsTrue(stopped);
        }

        private static IEnumerable<byte> ToBytes(IList<ArraySegment<byte>> bytesSent) {
            foreach (var segment in bytesSent) {
                if (segment.Array != null)
                    foreach (var b in segment)
                        yield return b;
            }
        }

        private class TestClient : IConnection, IActiveChannel
        {
            public Pipeline Pipeline;

            public TestClient(string id) => Id = id ?? throw new ArgumentNullException(nameof(id));

            public bool Active => true;
            public ulong Channel { get; private set; }
            public IConnection Connection => this;
            public string Id { get; }

            public IActiveChannel AllocateChannel(IChannelSink channelSink) => this;

            public void Dispose() => Stop();

            public IActiveChannel GetChannel(ulong channel) {
                Channel = channel;
                return this;
            }

            public bool Send(byte[] message) {
                Pipeline?.Send(new NetworkMessageSlice(Channel, message));
                return true;
            }

            public Task<Success> SinkAsync(byte[] message) => throw new NotImplementedException();

            public void Stop() {
                Pipeline?.Stop();
            }
        }

        private class TestSocket : ISocket
        {
            public TestSocket(CancellationTokenSource source, params byte[] bytesReceived) {
                _source = source;
                _bytesReceived = bytesReceived ?? throw new ArgumentNullException(nameof(bytesReceived));
            }

            public int Available => _bytesReceived.Length - _receivedCount;

            public IList<ArraySegment<byte>> BytesSent => bytesSent;
            public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 13013);

            public void Dispose() {
                // Do nothing
            }

            public async Task<int> ReceiveAsync(Memory<byte> memory, SocketFlags socketFlags, CancellationToken token) {
                await Task.Yield();
                if (_bytesReceived.Length > _receivedCount) {
                    int howMany = Math.Min(memory.Length, _bytesReceived.Length - _receivedCount);
                    Memory<byte> slice = _bytesReceived.Slice(_receivedCount, howMany);
                    if (slice.TryCopyTo(memory)) {
                        _receivedCount += slice.Length;
                        return slice.Length;
                    }
                }
                return 0;
            }

            public Task SendAsync(IList<ArraySegment<byte>> segment) {
                bytesSent.AddRange(segment);
                if (Available == 0)
                    _source.CancelAfter(100);
                return Task.CompletedTask;
            }

            public void Shutdown(SocketShutdown how) {
                // Do nothing
            }

            public void Stop() => throw new NotImplementedException();

            private readonly Memory<byte> _bytesReceived;
            private readonly CancellationTokenSource _source;
            private readonly List<ArraySegment<byte>> bytesSent = new List<ArraySegment<byte>>();
            private int _receivedCount;
        }
    }
}