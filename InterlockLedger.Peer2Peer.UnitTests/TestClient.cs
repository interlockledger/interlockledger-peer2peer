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

using System;
using System.Buffers;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class TestClient : AbstractDisposable, IConnection, IActiveChannel
    {
        public Pipeline Pipeline;

        public TestClient(string id) => Id = id.Required(nameof(id));

        public event Action<INetworkIdentity> ConnectionStopped;

        public bool Active => true;
        public bool CanReconnect => false;
        public ulong Channel { get; private set; }
        public bool Connected => Pipeline?.Connected ?? false;
        public IConnection Connection => this;
        public string Id { get; }
        public int InactivityTimeoutInMinutes { get; }
        public bool KeepReconnected { get; set; }
        public int ListeningBufferSize => 1024;
        public ulong LivenessMessageTag { get; }
        public int MaxConcurrentConnections { get; }
        public ulong MessageTag { get; }
        public string NetworkName { get; }
        public string NetworkProtocolName { get; }
        public bool NothingToSend => Pipeline?.NothingToSend ?? false;
        public bool Stopped => Pipeline?.Stopped ?? false;

        public IActiveChannel AllocateChannel(IChannelSink channelSink) => this;

        public IActiveChannel GetChannel(ulong channel) {
            Channel = channel;
            return this;
        }

        public async Task<bool> SendAsync(ReadOnlySequence<byte> messageBytes) {
            if (!messageBytes.IsEmpty)
                await Pipeline?.SendAsync(new NetworkMessageSlice(Channel, messageBytes));
            return true;
        }

        public void SetDefaultSink(IChannelSink sink) => _sink = sink.Required(nameof(sink));

        public async Task<Success> SinkAsync(ReadOnlySequence<byte> messageBytes)
            => await (_sink?.SinkAsync(messageBytes, this) ?? Task.FromResult(Success.Next));

        public void Stop() => Pipeline?.Stop();

        internal void OnPipelineStopped() => ConnectionStopped?.Invoke(this);

        protected override void DisposeManagedResources() => Stop();

        private IChannelSink _sink;
    }
}