// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2021 InterlockLedger Network
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

using System.Net;
using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    public static class ILoggerFactoryExtensions
    {
        public static ILogger<T> NewLogger<T>(this ILoggerFactory loggerFactory)
        => (loggerFactory.Required(nameof(loggerFactory))).CreateLogger<T>();
    }

    public sealed class SocketFactory
    {
        public SocketFactory(ILoggerFactory loggerFactory, short portDelta, ushort howManyPortsToTry = 5) {
            _logger = loggerFactory.NewLogger<SocketFactory>();
            PortDelta = portDelta;
            HowManyPortsToTry = howManyPortsToTry;
        }

        public ushort HowManyPortsToTry { get; }
        public short PortDelta { get; }

        public Socket GetSocket(string name, ushort portNumber) {
            return ScanAvailable(GetAddresses(name), portNumber);

            IEnumerable<IPAddress> GetAddresses(string name) {
                try {
                    return IPAddress.TryParse(name, out var address)
                           ? (new IPAddress[] { address })
                           : Dns.GetHostEntry(name).AddressList.Where(ip => IsIPV4(ip.AddressFamily));
                } catch (SocketException e) {
                    _logger.LogError(e, "Couldn't get addresses for '{name}'", name);
                    return Enumerable.Empty<IPAddress>();
                }
                static bool IsIPV4(AddressFamily family) => family == AddressFamily.InterNetwork;
            }

            Socket ScanForSocket(IEnumerable<IPAddress> localaddrs, ushort port) {
                for (ushort tries = HowManyPortsToTry; tries > 0; tries--) {
                    foreach (var localaddr in localaddrs) {
                        var socket = BindSocket(localaddr, port);
                        if (socket != null)
                            return socket;
                    }
                    port = (ushort)(port - PortDelta);
                }
                return null;

                Socket BindSocket(IPAddress localaddr, ushort port) {
                    try {
                        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        listenSocket.Bind(new IPEndPoint(localaddr, port));
                        listenSocket.Listen(120);
                        return listenSocket;
                    } catch (ArgumentOutOfRangeException aore) {
                        _logger.LogError(aore, "-- Bad port number while trying to bind a socket to listen at {localaddr}:{port}", localaddr, port);
                        return null;
                    } catch (SocketException e) {
                        _logger.LogError(e, "-- Error while trying to bind a socket to listen at {localaddr}:{port}", localaddr, port);
                        return null;
                    }
                }
            }

            Socket ScanAvailable(IEnumerable<IPAddress> localaddrs, ushort portNumber)
                => !localaddrs.Any() ? null : (ScanForSocket(localaddrs, portNumber) ?? ScanForSocket(localaddrs, 0));
        }

        private readonly ILogger _logger;
    }
}