/******************************************************************************************************************************

Copyright (c) 2018-2020 InterlockLedger Network
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
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class LivenessKeeper : AbstractDisposable
    {
        public LivenessKeeper(Func<ReadOnlySequence<byte>> buildAliveMessage, int inactivityTimeoutInMinutes, Func<IChannelSink, IActiveChannel> allocateChannel) {
            if (allocateChannel is null)
                throw new ArgumentNullException(nameof(allocateChannel));
            _buildAliveMessage = buildAliveMessage ?? throw new ArgumentNullException(nameof(buildAliveMessage));
            _activeChannel = allocateChannel(_sink) ?? throw new InvalidOperationException("Channel could not be allocated for LivenessKeeper");
            _timer = CreateTimer(Math.Max(inactivityTimeoutInMinutes, 1));

            Timer CreateTimer(int InactivityTimeoutInMinutes) {
                var minutesBeforeTimeout = TimeSpan.FromMinutes(InactivityTimeoutInMinutes);
                var dueTime = minutesBeforeTimeout.Subtract(TimeSpan.FromSeconds(35));
                var period = minutesBeforeTimeout.Divide(2);
                return new Timer(_ => KeepAlive(), null, dueTime, period);
            }
        }

        protected override void DisposeManagedResources() {
            _timer.Dispose();
            _activeChannel.Stop();
        }

        private static readonly IChannelSink _sink = new KeepAliveSink();
        private readonly IActiveChannel _activeChannel;
        private readonly Func<ReadOnlySequence<byte>> _buildAliveMessage;
        private readonly Timer _timer;

        private void KeepAlive() {
            if (_activeChannel.Active) {
                var aliveMessage = _buildAliveMessage();
                if (_activeChannel.SendAsync(aliveMessage).Result)
                    return;
            }
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private class KeepAliveSink : IChannelSink
        {
            public Task<Success> SinkAsync(ReadOnlySequence<byte> messageBytes, IActiveChannel channel) => Task.FromResult(Success.Next);
        }
    }
}