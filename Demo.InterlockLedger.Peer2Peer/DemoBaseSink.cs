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
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal abstract class DemoBaseSink : AbstractNodeSink, IChannelSink
    {
        public DemoBaseSink(string message, CancellationTokenSource source) {
            _source = PrepareConsole(message, source);
            PublishAtAddress = HostAtAddress = "localhost";
            PublishAtPortNumber = HostAtPortNumber = 8080;
            DefaultListeningBufferSize = 512;
            DefaultTimeoutInMilliseconds = 30_000;
            MessageTag = _messageTagCode;
            NetworkName = "Demo";
            NetworkProtocolName = "DemoPeer2Peer";
            NodeId = "Local Node";
        }

        public override IEnumerable<string> LocalResources { get; } = new string[] { "Document" };

        public override IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "TripleEcho" };

        public static byte[] AsUTF8Bytes(string s) => Encoding.UTF8.GetBytes(s);

        public override void HostedAt(string address, ushort port) {
            HostAtAddress = address;
            HostAtPortNumber = port;
        }

        public override void PublishedAt(string address, ushort port) {
            PublishAtAddress = address;
            PublishAtPortNumber = port;
        }

        public abstract void Run(IPeerServices peerServices);

        protected const ulong _messageTagCode = ':';
        protected static readonly byte[] _encodedMessageTag = _messageTagCode.ILIntEncode();
        protected static readonly IEnumerable<byte> _haveMoreMarker = new byte[] { 1 };
        protected static readonly IEnumerable<byte> _isLastMarker = new byte[] { 0 };
        protected readonly CancellationTokenSource _source;

        protected static NetworkMessageSlice ToMessage(IEnumerable<byte> bytes, bool isLast)
            => new NetworkMessageSlice(0, ToMessageBytes(bytes, isLast));

        protected static byte[] ToMessageBytes(IEnumerable<byte> bytes, bool isLast) {
            var prefixedBytes = (isLast ? _isLastMarker : _haveMoreMarker).Concat(bytes);
            var messagebytes = _encodedMessageTag.Concat(((ulong)prefixedBytes.Count()).ILIntEncode()).Concat(prefixedBytes).ToArray();
            return messagebytes;
        }

        private CancellationTokenSource PrepareConsole(string message, CancellationTokenSource source) {
            void Cancel(object sender, ConsoleCancelEventArgs e) {
                Console.WriteLine("Exiting...");
                source.Cancel();
            }
            Console.WriteLine(message);
            Console.TreatControlCAsInput = false;
            Console.CancelKeyPress += Cancel;
            return source;
        }

        public static string AsString(IEnumerable<byte> text) => Encoding.UTF8.GetString(text.ToArray());
    }
}