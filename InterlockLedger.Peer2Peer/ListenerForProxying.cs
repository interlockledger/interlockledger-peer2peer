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

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    public class ListenerForProxying : ListenerCommon, IListenerForProxying
    {
        public ListenerForProxying(string externalAddress, string hostedAddress, ushort firstPort, IConnection connection, SocketFactory socketFactory, CancellationTokenSource source, ILogger logger)
            : base(connection.Id, connection, CreateKindOfLinkedSource(source), logger) {
            ExternalAddress = externalAddress.Required(nameof(externalAddress));
            Connection = connection.Required(nameof(connection));
            _socket = socketFactory.GetSocket(hostedAddress, firstPort);
            if (_socket is null)
                throw new InterlockLedgerIOException($"Could not open a listening socket for proxying at {hostedAddress}:{firstPort}");
            ExternalPortNumber = (ushort)((IPEndPoint)_socket.LocalEndPoint).Port;
            HostedAddress = hostedAddress;
            _channelMap = new ConcurrentDictionary<string, ChannelPairing>();
            Sinked = LogSinked;
            Responded = LogResponded;
            Errored = LogError;
        }

        public IConnection Connection { get; }

        public Action<ReadOnlySequence<byte>, IActiveChannel, Exception> Errored { get; set; }

        public string HostedAddress { get; }

        public Action<ReadOnlySequence<byte>, IActiveChannel, ulong, bool> Responded { get; set; }

        public string Route => $"{ExternalAddress}:{ExternalPortNumber}";

        public Action<ReadOnlySequence<byte>, IActiveChannel, bool, ulong, bool> Sinked { get; set; }

#pragma warning disable CA2253 // Named placeholders should not be numeric values

        public void LogError(ReadOnlySequence<byte> message, IActiveChannel channel, Exception e)
            => _logger.LogError(e, "Error processing Message '{0}' from Channel {1}:{2}", message.ToUrlSafeBase64(), channel?.ToString() ?? "?", e.Message);

        public void LogResponded(ReadOnlySequence<byte> message, IActiveChannel channel, ulong externalChannelId, bool sent)
            => _logger.LogDebug("Responded with Message '{0}' from Channel {1} to External Channel {2}. Sent: {3}", message.ToUrlSafeBase64(), channel, externalChannelId, sent);

        public void LogSinked(ReadOnlySequence<byte> message, IActiveChannel channel, bool newPair, ulong proxiedChannelId, bool sent)
            => _logger.LogDebug("Sinked Message '{0}' from Channel {1} using {2} pair to Proxied Channel {3}. Sent: {4}", message.ToUrlSafeBase64(), channel, newPair ? "new" : "existing", proxiedChannelId, sent);

#pragma warning restore CA2253 // Named placeholders should not be numeric values

        public override Task<Success> SinkAsync(ReadOnlySequence<byte> messageBytes, IActiveChannel channel)
            => DoAsync(async () => {
                try {
                    if (_channelMap.TryGetValue(channel.Id, out var pair)) {
                        var sent = await pair.SendAsync(messageBytes);
                        Sinked(messageBytes, channel, false, pair.ProxiedChannelId, sent);
                    } else {
                        var newPair = new ChannelPairing(channel, Connection, this);
                        _channelMap.TryAdd(channel.Id, newPair);
                        var sent = await newPair.SendAsync(messageBytes);
                        Sinked(messageBytes, channel, true, newPair.ProxiedChannelId, sent);
                    }
                } catch (Exception e) {
                    Errored(messageBytes, channel, e);
                }
                return Success.Next;
            }, Success.Exit);

        public async Task<IListenerForProxying> StartAsync() {
            await StartListeningAsync();
            return this;
        }

        public override void Stop() {
            try {
                Connection.Stop();
                base.Stop();
            } catch {
            } finally {
                _channelMap.Clear();
            }
        }

        public override string ToString() => $"{nameof(ListenerForProxying)} {HeaderText} to connection {Connection.Id}";

        protected override string HeaderText => $"proxying {NetworkProtocolName} protocol in {NetworkName} network at {Route} hosted at {HostedAddress}";

        protected override string IdPrefix => "Proxying";

        protected override Socket BuildSocket() => _socket;

        protected override void DisposeManagedResources() {
            _channelMap.Clear();
            _socket.Dispose();
            base.DisposeManagedResources();
        }

        private readonly ConcurrentDictionary<string, ChannelPairing> _channelMap;
        private readonly Socket _socket;

        private static CancellationTokenSource CreateKindOfLinkedSource(CancellationTokenSource source) {
            var kindOfLinkedSource = new CancellationTokenSource();
            source.Token.Register(() => kindOfLinkedSource.Cancel(false));
            return kindOfLinkedSource;
        }

        private class ChannelPairing : IChannelSink, ISender
        {
            public ChannelPairing(IActiveChannel external, IConnection connection, ListenerForProxying parent) {
                _external = external.Required(nameof(external));
                _parent = parent.Required(nameof(parent));
                _tagAsILInt = new ReadOnlySequence<byte>(connection.MessageTag.AsILInt());
                _proxied = connection.AllocateChannel(this);
            }

            public ulong ProxiedChannelId => _proxied.Channel;

            public async Task<bool> SendAsync(ReadOnlySequence<byte> messageBytes) {
                return await SendThisAsync(messageBytes);

                async Task<bool> SendThisAsync(ReadOnlySequence<byte> messageBytes) {
                    try {
                        return await _proxied.SendAsync(PrependTagAndLength(messageBytes));
                    } catch (Exception e) {
                        _parent.Errored(messageBytes, _proxied, e);
                        return false;
                    }
                }
            }

            public async Task<Success> SinkAsync(ReadOnlySequence<byte> messageBytes, IActiveChannel channel) {
                return await SinkThisAsync(messageBytes, channel);

                async Task<Success> SinkThisAsync(ReadOnlySequence<byte> messageBytes, IActiveChannel channel) {
                    try {
                        var sent = await _external.SendAsync(PrependTagAndLength(messageBytes));
                        _parent.Responded(messageBytes, channel, _external.Channel, sent);
                    } catch (Exception e) {
                        _parent.Errored(messageBytes, channel, e);
                    }
                    return Success.Next;
                }
            }

            private readonly IActiveChannel _external;
            private readonly ListenerForProxying _parent;
            private readonly IActiveChannel _proxied;
            private readonly ReadOnlySequence<byte> _tagAsILInt;

            private ReadOnlySequence<byte> PrependTagAndLength(ReadOnlySequence<byte> message)
                => message.Prepend(((ulong)message.Length).AsILInt()).Prepend(_tagAsILInt);
        }
    }
}