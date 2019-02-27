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

namespace UnitTest.InterlockLedger.Peer2Peer
{
    [TestClass]
    public class UnitTestILIntReader
    {
        [TestMethod]
        public void Test0() => Assert.AreEqual(0ul, ILD(new byte[] { 0 }), "ILIntDecodeFromByteArray 0");

        [TestMethod]
        public void Test1() => Assert.AreEqual(1ul, ILD(new byte[] { 1 }), "ILIntDecodeFromByteArray 1");

        [TestMethod]
        public void Test1103823438329() => Assert.AreEqual(1103823438329ul, ILD(new byte[] { 0xFD, 1, 1, 1, 1, 1, 1 }), "ILIntDecodeFromByteArray 1103823438329");

        [TestMethod]
        public void Test128() => Assert.AreEqual(128ul, ILD(new byte[] { 0x80 }), "ILIntDecodeFromByteArray 128");

        [TestMethod]
        public void Test16843257() => Assert.AreEqual(16843257ul, ILD(new byte[] { 0xFB, 1, 1, 1, 1 }), "ILIntDecodeFromByteArray 16843257");

        [TestMethod]
        public void Test18446744073709551615() => Assert.AreEqual(18446744073709551615ul, ILD(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 7 }), "ILIntDecodeFromByteArray 18446744073709551615");

        [TestMethod]
        public void Test247() => Assert.AreEqual(247ul, ILD(new byte[] { 0xF7 }), "ILIntDecodeFromByteArray 247");

        [TestMethod]
        public void Test248() => Assert.AreEqual(248ul, ILD(new byte[] { 0xF8, 0 }), "ILIntDecodeFromByteArray 248");

        [TestMethod]
        public void Test249() => Assert.AreEqual(249ul, ILD(new byte[] { 0xF8, 1 }), "ILIntDecodeFromByteArray 249");

        [TestMethod]
        public void Test282578800148985() => Assert.AreEqual(282578800148985ul, ILD(new byte[] { 0xFE, 1, 1, 1, 1, 1, 1, 1 }), "ILIntDecodeFromByteArray 282578800148985");

        [TestMethod]
        public void Test4311810553() => Assert.AreEqual(4311810553ul, ILD(new byte[] { 0xFC, 1, 1, 1, 1, 1 }), "ILIntDecodeFromByteArray 4311810553");

        [TestMethod]
        public void Test503() => Assert.AreEqual(503ul, ILD(new byte[] { 0xF8, 0xFF }), "ILIntDecodeFromByteArray 503");

        [TestMethod]
        public void Test505() => Assert.AreEqual(505ul, ILD(new byte[] { 0xF9, 1, 1 }), "ILIntDecodeFromByteArray 505");

        [TestMethod]
        public void Test66041() => Assert.AreEqual(66041ul, ILD(new byte[] { 0xFA, 1, 1, 1 }), "ILIntDecodeFromByteArray 66041");

        [TestMethod]
        public void Test72057594037928183() => Assert.AreEqual(72057594037928183ul, ILD(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }), "ILIntDecodeFromByteArray 72057594037928183");

        [TestMethod]
        public void Test72340172838076921() => Assert.AreEqual(72340172838076921ul, ILD(new byte[] { 0xFF, 1, 1, 1, 1, 1, 1, 1, 1 }), "ILIntDecodeFromByteArray 72340172838076921");

        [TestMethod]
        public void TestTooLargeBy1() => Assert.ThrowsException<InvalidOperationException>(() => ILD(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 8 }), "ILIntDecodeFromByteArray too large by 1 => zero");

        [TestMethod]
        public void TestTooLargeBy120() => Assert.ThrowsException<InvalidOperationException>(() => ILD(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x80 }), "ILIntDecodeFromByteArray too large by 120 => zero");

        [TestMethod]
        public void TestTooLargeByAll() => Assert.ThrowsException<InvalidOperationException>(() => ILD(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }), "ILIntDecodeFromByteArray too large by all => zero");

        private ulong ILD(byte[] bytes) {
            var reader = new ILIntReader();
            for (int i = 0; i < bytes.Length; i++) {
                var done = reader.Done(bytes[i]);
                if (i + 1 < bytes.Length && done)
                    Assert.Fail("Value decoded without all bytes");
                if (i + 1 == bytes.Length && !done)
                    Assert.Fail("Value not decoded with supplied bytes");
            }
            return reader.Value;
        }
    }
}