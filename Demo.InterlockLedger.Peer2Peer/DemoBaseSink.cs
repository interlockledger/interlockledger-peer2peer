// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2022 InterlockLedger Network
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

using InterlockLedger.Peer2Peer;
using InterlockLedger.Tags;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Demo.InterlockLedger.Peer2Peer
{
    internal abstract class DemoBaseSink : AbstractNodeSink, IChannelSink
    {
        public override IEnumerable<string> LocalResources { get; } = new string[] { "Document" };

        public override IEnumerable<string> SupportedNetworkProtocolFeatures { get; } = new string[] { "Echo", "Who", "TripleEcho" };

        public static string AsString(ReadOnlySequence<byte> text) => Encoding.UTF8.GetString(text.ToArray());

        public static ReadOnlySequence<byte> AsUTF8Bytes(string s) => new(Encoding.UTF8.GetBytes(s));

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
            var serviceProvider = Configure(this, _source, portDelta: -4);
            using var peerServices = serviceProvider.GetRequiredService<IPeerServices>();
            Run(peerServices);
        }

        protected static readonly ReadOnlyMemory<byte> _encodedMessageTag = _messageTagCode.ILIntEncode();

        protected static readonly ReadOnlyMemory<byte> _haveMoreMarker = new byte[] { 1 };

        protected static readonly ReadOnlyMemory<byte> _isLastMarker = new byte[] { 0 };
        protected readonly CancellationTokenSource _source;

        protected DemoBaseSink(string message) {
            PublishAtAddress = HostAtAddress = "localhost";
            PublishAtPortNumber = HostAtPortNumber = 8080;
            ListeningBufferSize = 512;
            DefaultTimeoutInMilliseconds = 30_000;
            MaxConcurrentConnections = 2;
            InactivityTimeoutInMinutes = 1;
            MessageTag = _messageTagCode;
            NetworkName = "Demo";
            NetworkProtocolName = "DemoPeer2Peer";
            NodeId = "Local Node";
            _message = message.Required(nameof(message));
            _source = new CancellationTokenSource();
        }

        protected abstract Func<ReadOnlySequence<byte>> AliveMessageBuilder { get; }

        protected static NetworkMessageSlice ToMessage(ReadOnlySequence<byte> bytes, bool isLast)
            => new(0, ToMessageBytes(bytes, isLast));

        protected static ReadOnlySequence<byte> ToMessageBytes(ReadOnlySequence<byte> bytes, bool isLast) {
            var prefixedBytes = (isLast ? _isLastMarker : _haveMoreMarker).ToArray().Concat(bytes.ToArray());
            var messagebytes = _encodedMessageTag.ToArray().Concat(((ulong)prefixedBytes.Count()).ILIntEncode()).Concat(prefixedBytes).ToArray();
            return new ReadOnlySequence<byte>(messagebytes);
        }

        protected override void DisposeManagedResources() => _source.Dispose();

        protected override void DisposeUnmanagedResources() { }

        protected abstract void Run(IPeerServices peerServices);

        private const ulong _messageTagCode = ':';
        private readonly string _message;

        private static ServiceProvider Configure(INetworkConfig config, CancellationTokenSource source, short portDelta) {
            source.Required(nameof(source));
            return new ServiceCollection()
                .AddLogging(builder =>
                    builder
                        .AddSimpleConsole(c => {
                            c.ColorBehavior = LoggerColorBehavior.Disabled;
                            c.SingleLine = false;
                            c.IncludeScopes = false;
                        })
                        .SetMinimumLevel(LogLevel.Information))
                .AddSingleton(sp => new SocketFactory(sp.GetRequiredService<ILoggerFactory>(), portDelta, howManyPortsToTry: 7))
                .AddSingleton<IExternalAccessDiscoverer, DummyExternalAccessDiscoverer>()
                .AddSingleton(sp =>
                    new PeerServices(
                        config.MessageTag, config.LivenessMessageTag, config.NetworkName, config.NetworkProtocolName, config.ListeningBufferSize,
                        sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<IExternalAccessDiscoverer>(),
sp.GetRequiredService<SocketFactory>(), 10, 2).WithCancellationTokenSource(source))
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