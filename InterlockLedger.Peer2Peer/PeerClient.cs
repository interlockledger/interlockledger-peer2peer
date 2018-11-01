/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
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

        private const int _receiveTimeout = 30000;
        private const int _sleepStep = 10;

        public void Send(Span<byte> bytes, IMessageProcessor messageProcessor) {
            try {
                var messageParser = new MessageParser(_tag, messageProcessor, _logger);
                byte[] buffer = new byte[4096];
                using (var client = new TcpClient()) {
                    client.Connect(_networkAddress, _networkPort);
                    client.ReceiveTimeout = _receiveTimeout;
                    client.NoDelay = true;
                    using (NetworkStream stream = client.GetStream()) {
                        stream.Write(bytes.ToArray(), 0, bytes.Length);
                        stream.Flush();
                        do {
                            WaitForData(stream, waitForever: messageProcessor.AwaitMultipleAnswers);
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                                messageParser.Parse(new ReadOnlySequence<byte>(buffer, 0, bytesRead));
                        } while (messageParser.Continue);
                    }
                }
            } catch (SocketException se) {
                _logger.LogError($"Client could not connect into address {_networkAddress}:{_networkPort}.{Environment.NewLine}{se.Message}");
            }
        }

        private static void WaitForData(NetworkStream stream, bool waitForever) {
            int timeout = _receiveTimeout / _sleepStep;
            while (!stream.DataAvailable && (timeout > 0)) {
                Thread.Sleep(_sleepStep);
                if (!waitForever)
                    timeout--;
            }
        }


        private readonly string _networkAddress;
        private readonly int _networkPort;
        private readonly ulong _tag;
        private readonly ILogger _logger;

        public string Id { get; }
    }
}