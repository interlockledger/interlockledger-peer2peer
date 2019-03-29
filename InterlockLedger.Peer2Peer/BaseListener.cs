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
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    internal abstract class BaseListener : IDisposable
    {
        // TODO: Implement something more like Kestrel does for scaling up multiple simultaneous requests processing
        public BaseListener(CancellationTokenSource source, ILogger logger, int defaultListeningBufferSize) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _token = source.Token;
            _token.Register(Stop);
            _minimumBufferSize = Math.Max(512, defaultListeningBufferSize);
        }

        public void Dispose() => Stop();

        public async Task ListenOn(Socket socket) {
            _logger.LogTrace($"[{socket.RemoteEndPoint}]: connected");
            var pipe = new Pipe();
            Task writing = PipeFillAsync(socket, pipe.Writer);
            Task reading = PipeReadAsync(socket, pipe.Reader);
            await Task.WhenAll(reading, writing);
            _logger.LogTrace($"[{socket.RemoteEndPoint}]: disconnected");
        }

        public abstract void Stop();

        protected readonly ILogger _logger;
        protected readonly CancellationTokenSource _source;
        protected abstract ulong MessageTag { get; }

        protected abstract void LogHeader(string verb);

        protected void LogHeader(string verb, string protocolName, string networkName, string route)
            => _logger.LogInformation($"-- {verb} listening {protocolName} protocol in {networkName} network at {route}!");

        protected abstract Success Processor(IEnumerable<ReadOnlyMemory<byte>> bytes, ulong channel, Responder responder);

        private readonly int _minimumBufferSize;
        private readonly CancellationToken _token;

        private async Task PipeFillAsync(Socket socket, PipeWriter writer) {
            while (!_token.IsCancellationRequested) {
                try {
                    _logger.LogTrace($"Getting {_minimumBufferSize} bytes to receive in the socket");
                    Memory<byte> memory = writer.GetMemory(_minimumBufferSize);
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0) {
                        break;
                    }
                    // Tell the PipeWriter how much was read
                    writer.Advance(bytesRead);
                } catch {
                    break;
                }
                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();
                if (result.IsCompleted) {
                    break;
                }
            }
            // Signal to the reader that we're done writing
            writer.Complete();
        }

        private async Task PipeReadAsync(Socket socket, PipeReader reader) {
            var responder = new SocketResponder(socket);
            var parser = new MessageParser(MessageTag, _logger, (bytes, channel) => Processor(bytes, channel, responder));
            while (!_token.IsCancellationRequested) {
                ReadResult result = await reader.ReadAsync(_token);
                if (result.IsCanceled)
                    break;
                try {
                    reader.AdvanceTo(parser.Parse(result.Buffer));
                } catch (Exception e) {
                    _logger.LogError(e, "While parsing message");
                    reader.Complete(e);
                    return;
                }
                if (result.IsCompleted)
                    break;
            }
            reader.Complete();
        }
    }
}
