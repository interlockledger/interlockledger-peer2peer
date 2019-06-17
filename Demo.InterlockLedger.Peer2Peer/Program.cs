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

using InterlockLedger.Peer2Peer;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace Demo.InterlockLedger.Peer2Peer
{
    public static class Program
    {
        public static void Main(string[] args) {
            Console.WriteLine("Demo.InterlockLedger.Peer2Peer!");
            var source = new CancellationTokenSource();
            using (var peerServices = BuildPeerServices(source, new LoggerFactory().AddConsole(LogLevel.Information))) {
                if (args.Length > 0 && args[0].Equals("server", StringComparison.OrdinalIgnoreCase))
                    new DemoServer(source).Run(peerServices);
                else
                    new DemoClient(source).Run(peerServices);
            }
            Console.WriteLine("-- Done!");
        }

        private static IExternalAccessDiscoverer BuildDiscoverer(ILoggerFactory loggerFactory)
            => new DummyExternalAccessDiscoverer(new SocketFactory(loggerFactory, 4));

        private static IPeerServices BuildPeerServices(CancellationTokenSource source, ILoggerFactory loggerFactory)
            => new PeerServices(loggerFactory, BuildDiscoverer(loggerFactory)).WithCancellationTokenSource(source);
    }
}