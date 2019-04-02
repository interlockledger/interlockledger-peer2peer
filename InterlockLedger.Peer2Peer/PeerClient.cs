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
            _sinksLocker = new Locker(_source);
            MessageTag = tag;
            _networkAddress = networkAddress;
            _networkPort = port;
            _pipeline = null; // connect lazily
        }

        public string Id { get; }
        public bool IsDisposed { get; private set; } = false;
        public ulong LastChannelUsed => (ulong)_lastChannelUsed;

        public void Dispose() {
            if (!IsDisposed) {
                Stop();
                IsDisposed = true;
            }
        }

        public bool Send(IList<ArraySegment<byte>> segments, IClientSink clientSink) {
            if (Abandon)
                return false;
            try {
                if (!segments.IsEmpty()) {
                    if (_pipeline is null) {
                        _sender = new Sender();
                        _pipeline = Connect(_sender);
                    }
                    _sinksLocker.WithLock(() => {
                        Interlocked.Increment(ref _lastChannelUsed);
                        _sinks[LastChannelUsed] = (LastChannelUsed, clientSink);
                        _sender.Send(new Response(LastChannelUsed, segments));
                    });
                }
                return true;
            } catch (PeerException pe) {
                LogError(pe.Message);
            } catch (SocketException se) {
                LogError($"Client could not communicate with address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            } catch (TaskCanceledException) {
                // just ignore
            } catch (Exception e) {
                LogError($"Unexpected exception : {e}");
            }
            return false;
        }

        public override void Stop() {
            _pipeline?.Stop();
            _pipeline = null;
        }

        protected override ulong MessageTag { get; }

        protected override void PipelineStopped() {
            _logger.LogTrace($"Stopping pipeline on client to address {_networkAddress}:{_networkPort}");
            Stop();
        }

        protected override Success Processor(IEnumerable<ReadOnlyMemory<byte>> bytes, ulong channel, Sender responder)
            => _sinksLocker.WithLockAsync(async () => {
                if (_sinks.ContainsKey(channel)) {
                    var result = await _sinks[channel].sink.SinkAsClientAsync(bytes, channel);
                    if (result == Success.Exit) {
                        _sinks.Remove(channel);
                        return Success.Next;
                    }
                    return result;
                }
                return Success.Next;
            }).Result;

        private const int _hoursOfSilencedDuplicateErrors = 8;
        private const int _maxReceiveTimeout = 300_000;
        private static readonly Dictionary<string, DateTimeOffset> _errors = new Dictionary<string, DateTimeOffset>();
        private readonly string _networkAddress;
        private readonly int _networkPort;
        private readonly Dictionary<ulong, (ulong channel, IClientSink sink)> _sinks = new Dictionary<ulong, (ulong channel, IClientSink sink)>();
        private readonly Locker _sinksLocker;
        private long _lastChannelUsed = 0;
        private Pipeline _pipeline;
        private Sender _sender;
        private bool Abandon => _source.IsCancellationRequested || IsDisposed;

        private Pipeline Connect(Sender responder) {
            try {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(_networkAddress);
                IPAddress ipAddress = ipHostInfo.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(ipAddress, _networkPort));
                socket.ReceiveTimeout = _maxReceiveTimeout;
                socket.LingerState = new LingerOption(false, 1);
                var pipeline = CreatePipeline(socket, responder);
                _logger.LogTrace($"Client connecting into address {_networkAddress}:{_networkPort}");
                new Thread(async () => await pipeline.Listen()).Start();
                return pipeline;
            } catch (Exception se) {
                throw new PeerException($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}", se);
            }
        }

        private void LogError(string message) {
            if (!(_errors.TryGetValue(message, out var dateTime) && (DateTimeOffset.Now - dateTime).Hours < _hoursOfSilencedDuplicateErrors)) {
                _logger.LogError(message);
                _errors[message] = DateTimeOffset.Now;
            }
        }
    }
}