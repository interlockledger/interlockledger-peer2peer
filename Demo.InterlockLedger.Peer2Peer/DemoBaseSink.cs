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
using InterlockLedger.Tags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal abstract class DemoBaseSink : AbstractNodeSink, IChannelSink
    {
        protected DemoBaseSink(string message) {
            PublishAtAddress = HostAtAddress = "localhost";
            PublishAtPortNumber = HostAtPortNumber = 8080;
            ListeningBufferSize = 512;
            DefaultTimeoutInMilliseconds = 30_000;
            MessageTag = _messageTagCode;
            NetworkName = "Demo";
            NetworkProtocolName = "DemoPeer2Peer";
            NodeId = "Local Node";
            _message = message ?? throw new ArgumentNullException(nameof(message));
            _source = new CancellationTokenSource();
        }

        public override IEnumerable<string> LocalResources { get; } = new string[] { "Document" };
        public override IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "TripleEcho" };

        public static string AsString(IEnumerable<byte> text) => Encoding.UTF8.GetString(text.ToArray());

        public static byte[] AsUTF8Bytes(string s) => Encoding.UTF8.GetBytes(s);

        public override void HostedAt(string address, ushort port) {
            HostAtAddress = address;
            HostAtPortNumber = port;
        }

        public override void PublishedAt(string address, ushort port) {
            PublishAtAddress = address;
            PublishAtPortNumber = port;
        }

        public void Run() {
            PrepareConsole(_message);
            var serviceProvider = Configure(_source, portDelta: 4, this);
            using var peerServices = serviceProvider.GetRequiredService<IPeerServices>();
            Run(peerServices);
        }

        protected static readonly byte[] _encodedMessageTag = _messageTagCode.ILIntEncode();
        protected static readonly IEnumerable<byte> _haveMoreMarker = new byte[] { 1 };
        protected static readonly IEnumerable<byte> _isLastMarker = new byte[] { 0 };

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed at DisposeManagedResources")]
        protected readonly CancellationTokenSource _source;

        protected static NetworkMessageSlice ToMessage(IEnumerable<byte> bytes, bool isLast)
            => new NetworkMessageSlice(0, ToMessageBytes(bytes, isLast));

        protected static byte[] ToMessageBytes(IEnumerable<byte> bytes, bool isLast) {
            var prefixedBytes = (isLast ? _isLastMarker : _haveMoreMarker).Concat(bytes);
            var messagebytes = _encodedMessageTag.Concat(((ulong)prefixedBytes.Count()).ILIntEncode()).Concat(prefixedBytes).ToArray();
            return messagebytes;
        }

        protected override void DisposeManagedResources() => _source.Dispose();

        protected override void DisposeUnmanagedResources() { }

        protected abstract void Run(IPeerServices peerServices);

        private const ulong _messageTagCode = ':';
        private readonly string _message;

        private static ServiceProvider Configure(CancellationTokenSource source, ushort portDelta, INetworkConfig config) {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            return new ServiceCollection()
                .AddLogging(builder =>
                    builder
                        .AddConsole(c => c.DisableColors = false)
                        .SetMinimumLevel(LogLevel.Information))
                .AddSingleton(sp => new SocketFactory(sp.GetRequiredService<ILoggerFactory>(), portDelta))
                .AddSingleton<IExternalAccessDiscoverer, DummyExternalAccessDiscoverer>()
                .AddSingleton(sp =>
                    new PeerServices(
                        config.MessageTag, config.NetworkName, config.NetworkProtocolName, config.ListeningBufferSize,
                        sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<IExternalAccessDiscoverer>(),
                        sp.GetRequiredService<SocketFactory>()).WithCancellationTokenSource(source))
                .BuildServiceProvider();
        }

        private CancellationTokenSource PrepareConsole(string message) {
            void Cancel(object sender, ConsoleCancelEventArgs e) {
                Console.WriteLine("Exiting...");
                _source.Cancel();
            }
            Console.WriteLine(message);
            Console.TreatControlCAsInput = false;
            Console.CancelKeyPress += Cancel;
            return _source;
        }
    }
}