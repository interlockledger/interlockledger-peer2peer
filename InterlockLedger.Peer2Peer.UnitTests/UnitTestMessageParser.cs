// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2021 InterlockLedger Network
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

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InterlockLedger.Peer2Peer
{
    [TestClass]
    public sealed class UnitTestMessageParser : ILogger, IDisposable
    {
        [TestMethod]
        public void Creation() {
            static Task<Success> messageProcessor(NetworkMessageSlice channelBytes) => Task.FromResult(Success.Exit);

            var mp = new MessageParser(15, this, messageProcessor);
            Assert.IsNotNull(mp);
            Assert.ThrowsException<ArgumentException>(() => new MessageParser(15, null, messageProcessor));
            Assert.ThrowsException<ArgumentException>(() => new MessageParser(15, this, null));
        }

        [TestMethod]
        public void Parsing() => DoNiceParsing(7, 15, 3, 1, 2, 3, 7);

        [TestMethod]
        public void ParsingOffsetBy1ByteAndBegginingOfSecondMessage()
            => DoRawParsing(new ulong[] { 7 }, ToSequences(new byte[] { 1, 15, 3, 1, 2, 3, 7, 15, 1 }), 15, new byte[] { 1, 2, 3 });

        [TestMethod]
        public void ParsingOffsetBy3Bytes()
            => DoRawParsing(new ulong[] { 7 }, ToSequences(new byte[] { 1, 2, 3, 15, 3, 1, 2, 3, 7 }), 15, new byte[] { 1, 2, 3 });

        [TestMethod]
        public void ParsingTwoMessagesInDifferentChannels()
            => DoRawParsing(new ulong[] { 7, 13 }, ToSequences(new byte[] { 15, 3, 1, 2, 3, 7, 15, 1, 10, 13 }), 15, new byte[] { 1, 2, 3 }, new byte[] { 10 });

        [TestMethod]
        public void ParsingTwoMessagesInDifferentChannelsMultipleSequences()
            => DoRawParsing(new ulong[] { 7, 13 }, ToSequences(new byte[] { 15, 3, 1, 2 }, new byte[] { 3, 7, 15, 1, 10, 13 }), 15, new byte[] { 1, 2, 3 }, new byte[] { 10 });

        IDisposable ILogger.BeginScope<TState>(TState state) => this;

        public void Dispose() {
            // DO NOTHING
        }

        bool ILogger.IsEnabled(LogLevel logLevel) => logLevel > LogLevel.Warning;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
            // DO NOTHING
        }

        private static IEnumerable<ReadOnlySequence<byte>> ToSequences(params byte[][] arraysToParse) => arraysToParse.Select(a => new ReadOnlySequence<byte>(a));

        private void DoNiceParsing(ulong expectedChannel, params byte[] arrayToParse)
            => DoRawParsing(new ulong[] { expectedChannel }, ToSequences(arrayToParse), arrayToParse[0], arrayToParse.Skip(2).SkipLast(1).ToArray());

        private void DoRawParsing(ulong[] expectedChannels, IEnumerable<ReadOnlySequence<byte>> sequencesToParse, ulong expectedTag, params byte[][] expectedPayloads) {
            var results = new List<NetworkMessageSlice>();
            Task<Success> messageProcessor(NetworkMessageSlice channelBytes) {
                results.Add(channelBytes);
                return Task.FromResult(results.Count < expectedPayloads.Length ? Success.Next : Success.Exit);
            }
            var mp = new MessageParser(expectedTag, this, messageProcessor);
            foreach (var sequence in sequencesToParse) {
                mp.Parse(sequence);
            }
            int payloadIndex = 0;
            foreach (var channelBytes in results) {
                var inputBytes = channelBytes.DataList;
                Assert.AreEqual(expectedChannels[payloadIndex], channelBytes.Channel);
                byte[] expectedPayload = expectedPayloads[payloadIndex++];
                Assert.AreEqual(expectedPayload.Length, inputBytes.Length);
                Assert.IsTrue(expectedPayload.SequenceEqual(inputBytes.ToArray()));
            }
        }
    }
}