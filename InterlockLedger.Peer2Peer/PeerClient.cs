/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace InterlockLedger.Peer2Peer
{
    internal class PeerClient : IClient
    {
        public PeerClient(string id, string networkAddress, int port, ulong tag, ILogger logger) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(networkAddress))
                throw new ArgumentNullException(nameof(networkAddress));
            _networkAddress = networkAddress;
            _networkPort = port;
            _tag = tag;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Id = id;
        }

        public string Id { get; }

        public void Send(IList<ArraySegment<byte>> segments, IClientSink messageProcessor) {
            Socket sender = Connect();
            if (sender != null) {
                Send(segments, messageProcessor, sender, waitForever: false);
                sender.Shutdown(SocketShutdown.Both);
                sender.Close();
            }
        }

        public void Send(IList<ArraySegment<byte>> segments, IClientSink messageProcessor, Socket sender, bool waitForever) {
            try {
                var messageParser = new MessageParser(_tag, (bytes) => messageProcessor.SinkAsClientAsync(bytes).Result, _logger);
                byte[] buffer = new byte[4096];
                sender.Send(segments);
                do {
                    WaitForData(sender, waitForever);
                    int bytesRead = sender.Receive(buffer);
                    if (bytesRead > 0)
                        messageParser.Parse(new ReadOnlySequence<byte>(buffer, 0, bytesRead));
                } while (messageParser.Continue);
            } catch (SocketException se) {
                _logger.LogError($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            } catch (Exception e) {
                _logger.LogError("Unexpected exception : {0}", e.ToString());
            }
        }

        private const int _receiveTimeout = 30000;
        private const int _sleepStep = 10;
        private readonly ILogger _logger;

        private readonly string _networkAddress;

        private readonly int _networkPort;

        private readonly ulong _tag;

        private static void WaitForData(Socket socket, bool waitForever) {
            int timeout = _receiveTimeout / _sleepStep;
            while (socket.Available == 0 && (timeout > 0)) {
                Thread.Sleep(_sleepStep);
                if (!waitForever)
                    timeout--;
            }
        }

        private Socket Connect() {
            try {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(_networkAddress);
                IPAddress ipAddress = ipHostInfo.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                // Create a TCP/IP  socket.
                var sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sender.Connect(new IPEndPoint(ipAddress, _networkPort));
                sender.ReceiveTimeout = _receiveTimeout;
                return sender;
            } catch (SocketException se) {
                _logger.LogError($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            }
            return null;
        }
    }
}