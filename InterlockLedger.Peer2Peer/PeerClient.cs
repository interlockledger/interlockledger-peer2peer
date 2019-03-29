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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal sealed class PeerClient : BaseListener, IClient
    {
        public PeerClient(string id,
            string networkAddress,
            int port,
            ulong tag,
            CancellationTokenSource source,
            ILogger logger,
            int defaultListeningBufferSize) : base(source, logger, defaultListeningBufferSize) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(networkAddress))
                throw new ArgumentNullException(nameof(networkAddress));
            Id = id;
            _locked = 0;
            MessageTag = tag;
            _networkAddress = networkAddress;
            _networkPort = port;
            _socket = null; // connect lazily
        }

        public string Id { get; }
        public bool IsDisposed { get; private set; } = false;
        public ulong LastChannelUsed => (ulong)_lastChannelUsed;
        public int SocketHashCode => _socket?.GetHashCode() ?? 0;

        public void Dispose() => Stop();

        public void Reconnect() {
            if (!IsDisposed) {
                WithLock(() => {
                    lock (_sinks) {
                        _socket?.Dispose();
                        _socket = Connect();
                        _sinks.Clear();
                    }
                }).Wait();
            }
        }

        public bool Send(IList<ArraySegment<byte>> segments, IClientSink clientSink)
            => SendAsync(segments, clientSink).Result;

        public void SendAndReceiveAll(IList<ArraySegment<byte>> segments, IClientSink clientSink)
            => SendAndReceiveAllAsync(segments, clientSink).Wait();

        public async Task SendAndReceiveAllAsync(IList<ArraySegment<byte>> segments, IClientSink clientSink) {
            clientSink.DoneReceiving = false;
            try {
                if (await SendAsync(segments, clientSink)) {
                    while (!clientSink.DoneReceiving)
                        await Task.Yield();
                }
            } finally {
                clientSink.DoneReceiving = true;
            }
        }

        public async Task<bool> SendAsync(IList<ArraySegment<byte>> segments, IClientSink clientSink)
            => await WithLock(() => SendAsyncCore(segments, clientSink));

        public override void Stop() {
            if (!IsDisposed) {
                WithLock(() => {
                    _socket?.Dispose();
                    _socket = null;
                    IsDisposed = true;
                }).Wait();
            }
        }

        protected override ulong MessageTag { get; }

        protected override Success Processor(IEnumerable<ReadOnlyMemory<byte>> bytes, ulong channel, Responder responder) {
            lock (_sinks) {
                if (_sinks.ContainsKey(channel)) {
                    var result = _sinks[channel].sink.SinkAsClientAsync(bytes, channel).Result;
                    if (result == Success.Exit) {
                        _sinks.Remove(channel);
                        return Success.Next;
                    }
                    return result;
                }
                return Success.Next;
            }
        }

        private const int _hoursOfSilencedDuplicateErrors = 8;
        private const int _maxReceiveTimeout = 300_000;
        private const int _sleepStep = 10;
        private static readonly Dictionary<string, DateTimeOffset> _errors = new Dictionary<string, DateTimeOffset>();
        private readonly int _defaultTimeoutInMilliseconds;
        private readonly string _networkAddress;
        private readonly int _networkPort;
        private readonly Dictionary<ulong, (ulong channel, IClientSink sink)> _sinks = new Dictionary<ulong, (ulong channel, IClientSink sink)>();
        private long _lastChannelUsed = 0;
        private int _locked = 0;
        private Socket _socket;
        private bool Abandon => _source.IsCancellationRequested || IsDisposed;

        private Socket Connect() {
            try {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(_networkAddress);
                IPAddress ipAddress = ipHostInfo.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(ipAddress, _networkPort));
                socket.ReceiveTimeout = _maxReceiveTimeout;
                socket.LingerState = new LingerOption(false, 1);
                new Thread(async () => await ListenOn(socket)).Start();
                return socket;
            } catch (Exception se) {
                LogError($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
                throw;
            }
        }

        private void LogError(string message) {
            if (!(_errors.TryGetValue(message, out var dateTime) && (DateTimeOffset.Now - dateTime).Hours < _hoursOfSilencedDuplicateErrors)) {
                _logger.LogError(message);
                _errors[message] = DateTimeOffset.Now;
            }
        }

        private async Task<bool> SendAsyncCore(IList<ArraySegment<byte>> segments, IClientSink clientSink) {
            if (Abandon)
                return false;
            try {
                if (_socket is null) {
                    _socket = Connect();
                }
                Interlocked.Increment(ref _lastChannelUsed);
                lock (_sinks)
                    _sinks[LastChannelUsed] = (LastChannelUsed, clientSink);
                await _socket.SendAsync(segments);
                await _socket.SendILint(LastChannelUsed);
                return true;
            } catch (SocketException se) {
                LogError($"Client could not communicate with address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            } catch (TaskCanceledException) {
                // just ignore
            } catch (Exception e) {
                LogError($"Unexpected exception : {e}");
            }
            return false;
        }

        private async Task<T> WithLock<T>(Func<Task<T>> action) {
            if (1 == Interlocked.Exchange(ref _locked, 1))
                await Task.Delay(1000, _source.Token);
            try {
                return await action();
            } finally {
                Interlocked.Exchange(ref _locked, 0);
            }
        }

        private async Task WithLock(Action action) {
            if (1 == Interlocked.Exchange(ref _locked, 1))
                await Task.Delay(1000, _source.Token);
            try {
                action();
            } finally {
                Interlocked.Exchange(ref _locked, 0);
            }
        }
    }
}
