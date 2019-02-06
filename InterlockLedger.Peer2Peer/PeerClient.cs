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
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal class PeerClient : IClient
    {
        public PeerClient(string id, string networkAddress, int port, ulong tag, CancellationTokenSource source, ILogger logger) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(networkAddress))
                throw new ArgumentNullException(nameof(networkAddress));
            _networkAddress = networkAddress;
            _networkPort = port;
            _tag = tag;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Id = id;
        }

        public string Id { get; }

        public void Send(IList<ArraySegment<byte>> segments, IClientSink clientSink) => SendAsync(segments, clientSink).Wait();

        public void Send(IList<ArraySegment<byte>> segments, IClientSink clientSink, Socket sender) => SendAsync(segments, clientSink, sender).Wait();

        public async Task SendAsync(IList<ArraySegment<byte>> segments, IClientSink clientSink) {
            if (!_source.IsCancellationRequested) {
                Socket sender = Connect();
                if (sender != null) {
                    await SendAsync(segments, clientSink, sender);
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
            }
        }

        public async Task SendAsync(IList<ArraySegment<byte>> segments, IClientSink clientSink, Socket sender) {
            if (_source.IsCancellationRequested)
                return;
            try {
                var messageParser = new MessageParser(_tag, (bytes) => clientSink.SinkAsClientAsync(bytes).Result, _logger);
                int minimumBufferSize = Math.Max(512, clientSink.DefaultListeningBufferSize);
                await sender.SendAsync(segments);
                do {
                    if (_source.IsCancellationRequested)
                        return;
                    await WaitForData(sender, clientSink.WaitForever);
                    var buffer = new byte[minimumBufferSize];
                    int bytesRead = await sender.ReceiveAsync(buffer, SocketFlags.None);
                    if (bytesRead > 0)
                        messageParser.Parse(new ReadOnlySequence<byte>(buffer, 0, bytesRead));
                } while (messageParser.Continue);
            } catch (SocketException se) {
                LogError($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            } catch (TaskCanceledException) {
                // just ignore
            } catch (Exception e) {
                LogError($"Unexpected exception : {e}");
            }
        }

        private const int _hoursOfSilencedDuplicateErrors = 8;
        private const int _receiveTimeout = 30000;
        private const int _sleepStep = 10;
        private static readonly Dictionary<string, DateTimeOffset> _errors = new Dictionary<string, DateTimeOffset>();
        private readonly ILogger _logger;
        private readonly string _networkAddress;
        private readonly int _networkPort;
        private readonly CancellationTokenSource _source;
        private readonly ulong _tag;

        private Socket Connect() {
            try {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(_networkAddress);
                IPAddress ipAddress = ipHostInfo.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                var sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sender.Connect(new IPEndPoint(ipAddress, _networkPort));
                sender.ReceiveTimeout = _receiveTimeout;
                return sender;
            } catch (SocketException se) {
                LogError($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            }
            return null;
        }

        private void LogError(string message) {
            if (!(_errors.TryGetValue(message, out var dateTime) && (DateTimeOffset.Now - dateTime).Hours < _hoursOfSilencedDuplicateErrors)) {
                _logger.LogError(message);
                _errors[message] = DateTimeOffset.Now;
            }
        }

        private async Task WaitForData(Socket socket, bool waitForever) {
            int timeout = _receiveTimeout / _sleepStep;
            while (socket.Available == 0 && (timeout > 0) && !_source.IsCancellationRequested) {
                await Task.Delay(_sleepStep, _source.Token);
                if (!waitForever)
                    timeout--;
            }
        }
    }
}