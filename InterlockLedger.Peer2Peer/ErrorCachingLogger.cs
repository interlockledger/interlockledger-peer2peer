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

#nullable enable

using System.Collections.Concurrent;

namespace InterlockLedger.Peer2Peer
{
    public record ErrorCachingLogger(Func<bool> AbandonNow, ILogger Logger)
    {

        private const int _hoursOfSilencedDuplicateErrors = 8;
        private const int _tooManyErrors = 500;
        private static readonly ConcurrentDictionary<string, DateTimeOffset> _errors = new();
        public async Task CleanOldestErrors() {
            while (!AbandonNow()) {
                if (_errors.Count > 100) {
                    var now = DateTimeOffset.Now;
                    var oldestErrors = _errors.ToArray()
                                              .OrderBy(pair => pair.Value)
                                              .Where(pair => (now - pair.Value).TotalHours > _hoursOfSilencedDuplicateErrors * 2);
                    Remove(oldestErrors);
                }
                await Task.Delay(5_000);
                if (!AbandonNow() && _errors.Count > _tooManyErrors) {
                    var oldestErrors = _errors.ToArray()
                                              .OrderBy(pair => pair.Value)
                                              .Take(_errors.Count - _tooManyErrors);
                    Remove(oldestErrors);
                }
                await Task.Delay(10_000);
            }
        }

        public void LogError(string message) {
            var now = DateTimeOffset.Now;
            if (_errors.TryGetValue(message, out var dateTime) && (now - dateTime).Hours < _hoursOfSilencedDuplicateErrors)
                return;
            _ = _errors.AddOrUpdate(message, m => LogAt(now, m), (m, lastMoment) => now > lastMoment ? LogAt(now, m) : lastMoment);
        }

        private DateTimeOffset LogAt(DateTimeOffset now, string message) {
            Logger.LogError("{now} - {message}", now, message);
            return now;
        }
        private static void Remove(IEnumerable<KeyValuePair<string, DateTimeOffset>> errorsToRemove) {
            foreach (var error in errorsToRemove) {
                if (!_errors.TryRemove(error))
                    return;
            }
        }
    }
}