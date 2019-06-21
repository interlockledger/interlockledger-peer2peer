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

using InterlockLedger.ILInt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class ListenerForProxying : ListenerCommon
    {
        public ListenerForProxying(ListenerForPeer referenceListener, ushort firstPort, ConnectionInitiatedByPeer connection, SocketFactory socketFactory, CancellationTokenSource source, ILogger logger)
            : base(referenceListener.Id, referenceListener.MessageTag, source, logger, connection.ListeningBufferSize) {
            _referenceListener = referenceListener ?? throw new ArgumentNullException(nameof(referenceListener));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _socket = socketFactory.GetSocket(referenceListener.ExternalAddress, firstPort);
            var port = (ushort)((IPEndPoint)_socket.LocalEndPoint).Port;
            _route = $"{referenceListener.ExternalAddress}:{port}!";
            _channelMap = new ConcurrentDictionary<string, ChannelPairing>();
        }

        public override Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel) {
            if (_channelMap.TryGetValue(channel.Id, out var pair)) {
                pair.Send(message);
            } else {
                var newPair = new ChannelPairing(channel, _connection, MessageTag);
                _channelMap.TryAdd(channel.Id, newPair);
                newPair.Send(message);
            }
            return Task.FromResult(Success.Next);
        }

        protected override string HeaderText
            => $"proxying {_referenceListener.NetworkProtocolName} protocol in {_referenceListener.NetworkName} network at {_route}!";

        protected override string IdPrefix => "Proxying";

        protected override Socket BuildSocket() => _socket;

        private readonly ConcurrentDictionary<string, ChannelPairing> _channelMap;
        private readonly ConnectionInitiatedByPeer _connection;
        private readonly ListenerForPeer _referenceListener;
        private readonly string _route;
        private readonly Socket _socket;

        private class ChannelPairing : IChannelSink, ISender
        {
            public ChannelPairing(IActiveChannel external, ConnectionInitiatedByPeer connection, ulong tag) {
                _external = external ?? throw new ArgumentNullException(nameof(external));
                _tagAsILInt = tag.AsILInt();
                _proxied = connection.AllocateChannel(this);
            }

            public bool Send(IEnumerable<byte> message) => _proxied.Send(WithTagAndLength(message));

            public Task<Success> SinkAsync(IEnumerable<byte> message, IActiveChannel channel) {
                _external.Send(WithTagAndLength(message));
                return Task.FromResult(Success.Next);
            }

            private readonly IActiveChannel _external;
            private readonly IActiveChannel _proxied;
            private readonly byte[] _tagAsILInt;

            private static byte[] LengthAsILInt(IEnumerable<byte> message)
                => ((ulong)message.LongCount()).AsILInt();

            private IEnumerable<byte> WithTagAndLength(IEnumerable<byte> message)
                => _tagAsILInt.Concat(LengthAsILInt(message)).Concat(message);
        }
    }
}