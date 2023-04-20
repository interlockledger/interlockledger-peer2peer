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

#nullable enable

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    public class Pipeline
    {
        public Pipeline(ISocket socket,
                        CancellationTokenSource source,
                        ulong messageTag,
                        ulong livenessMessageTag,
                        int minimumBufferSize,
                        Func<NetworkMessageSlice, Task<Success>> sliceProcessor,
                        Action stopProcessor,
                        ILogger logger,
                        int inactivityTimeoutInMinutes,
                        ConcurrentQueue<NetworkMessageSlice> queue) {
            _messageTag = messageTag;
            _livenessMessageTag = livenessMessageTag;
            if (_messageTag == _livenessMessageTag)
                throw new InvalidOperationException($"{nameof(messageTag)} (value {messageTag}) must be different from {nameof(livenessMessageTag)} (value {livenessMessageTag})");
            _socket = socket.Required(nameof(socket));
            _queue = queue.Required();
            _logger = logger.Required(nameof(logger));
            var parentSource = source.Required(nameof(source));
            _localSource = new CancellationTokenSource();
            _linkedToken = CancellationTokenSource.CreateLinkedTokenSource(parentSource.Token, _localSource.Token).Token;
            _minimumBufferSize = minimumBufferSize;
            _sliceProcessor = sliceProcessor.Required(nameof(sliceProcessor));
            _stopProcessor = stopProcessor.Required(nameof(stopProcessor));
            _inactivity = new TimeoutManager(inactivityTimeoutInMinutes, SendLivenessMessageAsync);
        }

        public bool Connected => _socket.Connected;
        public bool NothingToSend => _queue.IsEmpty;
        public bool Stopped { get; private set; } = false;

        public async Task ListenAsync() {
            try {
                if (Connected) {
                    await UsePipes(_socket.RemoteEndPoint);
                    await Task.Delay(10);
                }
            } finally {
                _stopping = true;
                _socket.Dispose();
                try {
                    _stopProcessor();
                } catch { }
                Stopped = true;
            }
        }

        public Task<bool> SendAsync(NetworkMessageSlice slice) {
            if (!slice.IsEmpty) {
                _queue.Enqueue(slice);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public void Stop() {
            _stopping = true;
            _localSource.Cancel(false);
        }

        private readonly TimeoutManager _inactivity;
        private readonly CancellationToken _linkedToken;
        private readonly ulong _livenessMessageTag;
        private readonly CancellationTokenSource _localSource;
        private readonly ILogger _logger;
        private readonly ulong _messageTag;
        private readonly int _minimumBufferSize;
        private readonly ConcurrentQueue<NetworkMessageSlice> _queue;
        private readonly Func<NetworkMessageSlice, Task<Success>> _sliceProcessor;
        private readonly ISocket _socket;
        private readonly Action _stopProcessor;
        private bool _stopping;

        private bool _active => !_stopping && !_linkedToken.IsCancellationRequested;

        private async Task LivenessProcessorAsync(ulong livenessCode) {
            if (livenessCode == 0)
                await _inactivity.RestartAsync();
            else
                await SendLivenessMessageAsync(0);
        }

        private async Task PipeFillAsync(PipeWriter writer) {
            while (_active) {
                try {
                    if (_socket.Available > 0) {
                        await _inactivity.RestartAsync();
                        _logger.LogTrace("Getting {_minimumBufferSize} bytes to receive in the socket", _minimumBufferSize);
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
                    } else {
                        await Task.Delay(1, _linkedToken);
                        if (_inactivity.TimedOut)
                            _localSource.Cancel(false);
                    }
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
                    if (_stopping)
                        break;
                    using (await senderWriterLock.LockAsync()) {
                        var response = await DequeueAsync(_linkedToken);
                        if (!await writer.WriteResponseAsync(response, _linkedToken))
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

            async Task<NetworkMessageSlice> DequeueAsync(CancellationToken token) {
                NetworkMessageSlice slice = default;
                while (!_stopping && !token.IsCancellationRequested && !_queue.TryDequeue(out slice)) {
                    await Task.Delay(1, token);
                }
                return slice;
            }
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
                            var bytesCount = await _socket.SendBuffersAsync(sequence, _linkedToken);
                            reader.AdvanceTo(sequence.End);
                            if (bytesCount < 0)
                                _localSource.Cancel(false);
                        }
                    }
                    if (_stopping)
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

        private Task SendLivenessMessageAsync(ulong livenessCode) {
            if (!_stopping)
                _queue.Enqueue(new NetworkMessageSlice(livenessCode, _livenessMessageTag));
            return Task.CompletedTask;
        }

        private Task UsePipes(EndPoint remoteEndPoint) {
            _logger.LogTrace("[{remoteEndPoint}]: connected", remoteEndPoint);
            try {
                var parser = new MessageParser(_messageTag, _livenessMessageTag, _logger, _sliceProcessor, LivenessProcessorAsync);
                var listeningPipe = new Pipe();
                var respondingPipe = new Pipe();
                return Task.WhenAll(
                    PipeFillAsync(listeningPipe.Writer),
                    PipeReadAsync(listeningPipe.Reader, parser),
                    SendingPipeDequeueAsync(respondingPipe.Writer),
                    SendingPipeSendAsync(respondingPipe.Reader)
                );
            } catch (OperationCanceledException) {
                // just ignore
                return Task.CompletedTask;
            } catch (Exception e) {
                _logger.LogError(e, "[{remoteEndPoint}]: Exception while processing on the pipeline", remoteEndPoint);
                return Task.FromException(e);
            }
        }
    }
}