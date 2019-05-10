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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal abstract class BasePeerClient : BaseListener, IResponder
    {
        public bool Send(NetworkMessageSlice slice, ISink clientSink = null) {
            if (Abandon)
                return false;
            try {
                if (!slice.IsEmpty) {
                    Pipeline.Send(AdjustSlice(slice, clientSink));
                }
                return true;
            } catch (PeerException pe) {
                LogError(pe.Message);
            } catch (SocketException se) {
                LogError($"Client could not communicate with address {NetworkAddress}:{NetworkPort}.{Environment.NewLine}{se.Message}");
            } catch (TaskCanceledException) {
                // just ignore
            } catch (Exception e) {
                LogError($"Unexpected exception : {e}");
            }
            return false;
        }

        public override void Stop() => _pipeline?.Stop();

        protected BasePeerClient(string id, ulong tag, CancellationTokenSource source, ILogger logger, int defaultListeningBufferSize)
            : base(id, tag, source, logger, defaultListeningBufferSize) => _pipeline = null;

        protected string NetworkAddress { get; set; }

        protected int NetworkPort { get; set; }

        protected abstract NetworkMessageSlice AdjustSlice(NetworkMessageSlice slice, ISink clientSink);

        protected abstract Socket BuildSocket();

        protected void LogError(string message) {
            if (!(_errors.TryGetValue(message, out var dateTime) && (DateTimeOffset.Now - dateTime).Hours < _hoursOfSilencedDuplicateErrors)) {
                _logger.LogError(message);
                _errors[message] = DateTimeOffset.Now;
            }
        }

        protected void StartPipeline() => _ = Pipeline;

        private const int _hoursOfSilencedDuplicateErrors = 8;
        private static readonly Dictionary<string, DateTimeOffset> _errors = new Dictionary<string, DateTimeOffset>();
        private Pipeline _pipeline;

        private Pipeline Pipeline {
            get {
                try {
                    if (_pipeline != null)
                        return _pipeline;
                    _pipeline = RunPipeline(BuildSocket(), this);
                    return _pipeline;
                } catch (Exception se) {
                    throw new PeerException($"Client {Id} could not connect into remote endpoint {NetworkAddress}:{NetworkPort} .{Environment.NewLine}{se.Message}", se);
                }
            }
        }

        private Pipeline RunPipeline(Socket socket, IResponder responder)
             => new Pipeline(new NetSocket(socket), responder, _source, _messageTag, _minimumBufferSize, SinkAsync, PipelineStopped, _logger)
                .Start($"Pipeline {Id} to {socket.RemoteEndPoint}");
    }
}