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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class Pipeline
    {
        public async Task Listen() {
            await UsePipes(_socket.RemoteEndPoint);
            _socket = null;
        }

        public void Stop() => _source.Cancel();

        internal Pipeline(Socket socket, Sender sender, ILogger logger, CancellationTokenSource source, ulong messageTag, int minimumBufferSize, Func<IEnumerable<ReadOnlyMemory<byte>>, ulong, Sender, Success> processor, Action stopProcessor) {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var parentSource = source ?? throw new ArgumentNullException(nameof(source));
            _source = new CancellationTokenSource();
            _token = CancellationTokenSource.CreateLinkedTokenSource(parentSource.Token, _source.Token).Token;
            _dequeueLocker = new Locker(_token);
            _messageTag = messageTag;
            _minimumBufferSize = minimumBufferSize;
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _stopProcessor = stopProcessor ?? throw new ArgumentNullException(nameof(stopProcessor));
        }

        private readonly Locker _dequeueLocker;
        private readonly ILogger _logger;
        private readonly ulong _messageTag;
        private readonly int _minimumBufferSize;
        private readonly Func<IEnumerable<ReadOnlyMemory<byte>>, ulong, Sender, Success> _processor;
        private readonly Sender _sender;
        private readonly CancellationTokenSource _source;
        private readonly Action _stopProcessor;
        private readonly CancellationToken _token;
        private Socket _socket;
        private bool Active => !_token.IsCancellationRequested;

        private async Task PipeFillAsync(PipeWriter writer) {
            while (Active) {
                try {
                    _logger.LogTrace($"Getting {_minimumBufferSize} bytes to receive in the socket");
                    Memory<byte> memory = writer.GetMemory(_minimumBufferSize);
                    int bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None);
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

        private async Task PipeReadAsync(PipeReader reader, MessageParser parser) {
            while (Active) {
                ReadResult result = await reader.ReadAsync(_token);
                if (result.IsCanceled)
                    break;
                try {
                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty) {
                        reader.AdvanceTo(parser.Parse(result.Buffer));
                    }
                } catch (Exception e) {
                    _logger.LogError(e, "While reading/parsing message");
                    reader.Complete(e);
                    _stopProcessor();
                    return;
                }
                if (result.IsCompleted)
                    break;
            }
            reader.Complete();
        }

        private async Task SendingPipeDequeueAsync(PipeWriter writer) {
            while (Active) {
                try {
                    if (_sender.Exit)
                        break;
                    Response response = await _sender.DequeueAsync(_token);
                    if (!await WriteResponse(writer, response))
                        break;
                } catch {
                    break;
                }
            }
            writer.Complete();
        }

        private async Task SendingPipeSendAsync(PipeReader reader) {
            while (Active) {
                ReadResult result = await reader.ReadAsync(_token);
                if (result.IsCanceled)
                    break;
                try {
                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty) {
                        foreach (var b in buffer)
                            await _socket.SendAsync(MemoryExtensions.GetArraySegment(b));
                        reader.AdvanceTo(buffer.End);
                    }
                } catch (Exception e) {
                    reader.Complete(e);
                    _stopProcessor();
                    return;
                }
                if (result.IsCompleted)
                    break;
            }
            reader.Complete();
        }

        private async Task UsePipes(EndPoint remoteEndPoint) {
            _logger.LogTrace($"[{remoteEndPoint}]: connected");
            try {
                var parser = new MessageParser(_messageTag, _logger, (bytes, channel) => _processor(bytes, channel, _sender));
                var listeningPipe = new Pipe();
                var respondingPipe = new Pipe();
                Task writing = PipeFillAsync(listeningPipe.Writer);
                Task reading = PipeReadAsync(listeningPipe.Reader, parser);
                Task responseWriting = SendingPipeDequeueAsync(respondingPipe.Writer);
                Task responseSending = SendingPipeSendAsync(respondingPipe.Reader);
                await Task.WhenAll(reading, writing, responseWriting, responseSending);
            } catch (OperationCanceledException) {
                // just ignore
            } catch (Exception e) {
                _logger.LogError(e, $"[{remoteEndPoint}]: Exception while processing on the pipeline");
            }
            _logger.LogTrace($"[{remoteEndPoint}]: disconnected");
        }

        private async Task<bool> WriteResponse(PipeWriter writer, Response response)
            => await _dequeueLocker.WithLockAsync(async () => {
                if (response.IsEmpty)
                    return true;
                foreach (var segment in response.DataList)
                    if ((await writer.WriteAsync(segment, _token)).IsCanceled)
                        return false;
                return !(await writer.WriteILintAsync(response.Channel, _token)).IsCanceled;
            });
    }
}