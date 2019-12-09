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

using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace InterlockLedger.Peer2Peer
{
    public class Pipeline
    {
        public Pipeline(ISocket socket, CancellationTokenSource source, ulong messageTag,
            int minimumBufferSize, Func<NetworkMessageSlice, Task<Success>> sliceProcessor,
            Action stopProcessor, ILogger logger) {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _queue = new SendingQueue();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var parentSource = source ?? throw new ArgumentNullException(nameof(source));
            _localSource = new CancellationTokenSource();
            _linkedToken = CancellationTokenSource.CreateLinkedTokenSource(parentSource.Token, _localSource.Token).Token;
            _messageTag = messageTag;
            _minimumBufferSize = minimumBufferSize;
            _sliceProcessor = sliceProcessor ?? throw new ArgumentNullException(nameof(sliceProcessor));
            _stopProcessor = stopProcessor ?? throw new ArgumentNullException(nameof(stopProcessor));
        }

        public bool Stopped { get; private set; } = false;
        public bool Connected => _socket.Connected;
        public bool NothingToSend => _queue.IsEmpty;

        public async Task ListenAsync() {
            try {
                await UsePipes(_socket.RemoteEndPoint);
                await Task.Delay(10);
            } finally {
                _socket.Dispose();
                try {
                    _stopProcessor();
                } catch { }
                Stopped = true;
            }
        }

        public void Send(NetworkMessageSlice slice) {
            if (!slice.IsEmpty)
                _queue.Enqueue(slice);
        }

        public Pipeline Start(string threadName) {
            ListenAsync().RunOnThread(threadName);
            return this;
        }

        public void Stop() {
            _queue.Stop();
            _localSource.Cancel(false);
        }

        private readonly CancellationToken _linkedToken;
        private readonly CancellationTokenSource _localSource;
        private readonly ILogger _logger;
        private readonly ulong _messageTag;
        private readonly int _minimumBufferSize;
        private readonly SendingQueue _queue;
        private readonly Func<NetworkMessageSlice, Task<Success>> _sliceProcessor;
        private readonly ISocket _socket;
        private readonly Action _stopProcessor;

        private bool _active => !_linkedToken.IsCancellationRequested;

        private async Task PipeFillAsync(PipeWriter writer) {
            while (_active) {
                try {
                    if (_socket.Available > 0) {
                        _logger.LogTrace($"Getting {_minimumBufferSize} bytes to receive in the socket");
                        var memory = writer.GetMemory(_minimumBufferSize);
                        int bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None, _linkedToken);
                        if (bytesRead > 0) {
                            // Tell the PipeWriter how much was read
                            writer.Advance(bytesRead);
                            // Make the data available to the PipeReader
                            var result = await writer.FlushAsync();
                            if (result.IsCompleted) {
                                break;
                            }
                        } else
                            break;
                    } else
                        await Task.Delay(1, _linkedToken);
                } catch (OperationCanceledException oce) {
                    writer.Complete(oce);
                    return;
                } catch (SocketException se) when (se.ErrorCode.In(10054, 104)) {
                    _localSource.Cancel(false);
                    writer.Complete(se);
                    return;
                } catch (Exception e) {
                    _logger.LogError(e, "While receiving from socket");
                    writer.Complete(e);
                    return;
                }
            }
            // Signal to the reader that we're done writing
            writer.Complete();
        }

        private async Task PipeReadAsync(PipeReader reader, MessageParser parser) {
            while (_active) {
                try {
                    var result = await reader.ReadAsync(_linkedToken);
                    if (result.IsCanceled)
                        break;
                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                        reader.AdvanceTo(parser.Parse(result.Buffer));
                    if (result.IsCompleted)
                        break;
                } catch (OperationCanceledException oce) {
                    reader.Complete(oce);
                    return;
                } catch (SocketException se) when (se.ErrorCode.In(10054, 104)) {
                    _localSource.Cancel(false);
                    reader.Complete(se);
                    return;
                } catch (Exception e) {
                    _logger.LogError(e, "While reading/parsing message");
                    reader.Complete(e);
                    return;
                }
            }
            reader.Complete();
        }

        private async Task SendingPipeDequeueAsync(PipeWriter writer) {
            var senderWriterLock = new AsyncLock();
            while (_active) {
                try {
                    if (_queue.Exit)
                        break;
                    var response = await _queue.DequeueAsync(_linkedToken);
                    using (await senderWriterLock.LockAsync()) {
                        if (!await WriteResponse(writer, response))
                            break;
                    }
                } catch (OperationCanceledException oce) {
                    writer.Complete(oce);
                    return;
                } catch (SocketException se) when (se.ErrorCode.In(10054, 104)) {
                    _localSource.Cancel(false);
                    writer.Complete(se);
                    return;
                } catch (Exception e) {
                    _logger.LogError(e, "While dequeueing");
                    writer.Complete(e);
                    return;
                }
            }
            writer.Complete();
        }

        private async Task SendingPipeSendAsync(PipeReader reader) {
            var senderSocketLock = new AsyncLock();
            while (_active) {
                try {
                    var result = await reader.ReadAsync(_linkedToken);
                    if (result.IsCanceled)
                        break;
                    var sequence = result.Buffer;
                    if (!sequence.IsEmpty) {
                        using (await senderSocketLock.LockAsync()) {
                            await _socket.SendAsync(sequence.ToArraySegments());
                            reader.AdvanceTo(sequence.End);
                        }
                    }
                    if (_queue.Exit)
                        _localSource.Cancel(false);
                    if (result.IsCompleted)
                        break;
                } catch (OperationCanceledException oce) {
                    reader.Complete(oce);
                    return;
                } catch (SocketException se) when (se.ErrorCode.In(10054, 104)) {
                    _localSource.Cancel(false);
                    reader.Complete(se);
                    return;
                } catch (Exception e) {
                    _logger.LogError(e, "While sending bytes in the socket");
                    reader.Complete(e);
                    return;
                }
            }
            reader.Complete();
        }

        private async Task UsePipes(EndPoint remoteEndPoint) {
            _logger.LogTrace($"[{remoteEndPoint}]: connected");
            try {
                var parser = new MessageParser(_messageTag, _logger, _sliceProcessor);
                var listeningPipe = new Pipe();
                var respondingPipe = new Pipe();
                var pipeTasks = new Task[] {
                    PipeFillAsync(listeningPipe.Writer),
                    PipeReadAsync(listeningPipe.Reader, parser),
                    SendingPipeDequeueAsync(respondingPipe.Writer),
                    SendingPipeSendAsync(respondingPipe.Reader)
                };
                await Task.WhenAll(pipeTasks);
                _logger.LogTrace($"[{remoteEndPoint}]: all pipes stopped");
            } catch (OperationCanceledException) {
                // just ignore
            } catch (Exception e) {
                _logger.LogError(e, $"[{remoteEndPoint}]: Exception while processing on the pipeline");
            }
            _logger.LogTrace($"[{remoteEndPoint}]: disconnected");
        }

        private async Task<bool> WriteResponse(PipeWriter writer, NetworkMessageSlice response) {
            if (response.IsEmpty)
                return true;
            foreach (var segment in response.DataList)
                if ((await writer.WriteAsync(segment, _linkedToken)).IsCanceled)
                    return false;
            return !(await writer.WriteILintAsync(response.Channel, _linkedToken)).IsCanceled;
        }
    }
}