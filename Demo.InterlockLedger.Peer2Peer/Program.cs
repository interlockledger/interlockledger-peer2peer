/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
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
        public static byte[] AsUTF8Bytes(this string s) => Encoding.UTF8.GetBytes(s);

        public static void Main(string[] args) {
            Console.WriteLine("Demo.InterlockLedger.Peer2Peer!");
            var factory = new LoggerFactory();
            factory.AddConsole(LogLevel.Information);
            _nodeSink = new DemoNodeSink();
            _peerServices = new PeerServices(factory, new DummyExternalAccessDiscoverer(factory));
            if (args.Length > 0 && args[0].Equals("server", StringComparison.OrdinalIgnoreCase))
                Server();
            else
                Client();
            Console.WriteLine("-- Done!");
        }

        public static void Server() {
            PrepareConsole("Server");
            using (var listener = _peerServices.CreateFor(_nodeSink, _cancellationSource)) {
                listener.Start();
                while (listener.Alive) {
                    Thread.Yield();
                }
            }
        }

        private static DemoNodeSink _nodeSink;
        private static PeerServices _peerServices;
        private static readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

        private static void Client() {
            PrepareConsole("Client");
            while (!_cancellationSource.IsCancellationRequested) {
                Console.Write(_nodeSink.Prompt);
                var command = Console.ReadLine();
                if (command == null || command.FirstOrDefault() == 'x')
                    break;
                var client = _peerServices.GetClient(_nodeSink.MessageTag, "server", "localhost", 8080, _cancellationSource);
                client.Send(_nodeSink.ToMessage(command.AsUTF8Bytes(), isLast: true), _nodeSink);
            }
        }

        private static void PrepareConsole(string message) {
            Console.WriteLine(message);
            Console.TreatControlCAsInput = false;
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => { Console.WriteLine("Exiting..."); _cancellationSource.Cancel(); };
        }
    }
}