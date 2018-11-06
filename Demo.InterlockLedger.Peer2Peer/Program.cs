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
        }

        public static void Server() {
            Console.WriteLine("Server");
            var source = new CancellationTokenSource();
            Console.TreatControlCAsInput = true;
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => { Console.WriteLine("Exiting..."); source.Cancel(); };
            using (var listener = _peerServices.CreateFor(_nodeSink, source)) {
                listener.Start();
                while (listener.Alive) {
                    Thread.Yield();
                }
            }
            Console.WriteLine(" Done!");
        }

        private static DemoNodeSink _nodeSink;
        private static PeerServices _peerServices;

        private static void Client() {
            Console.WriteLine("Client");
            while (true) {
                Console.Write(_nodeSink.Prompt);
                var command = Console.ReadLine();
                if (command.FirstOrDefault() == 'x')
                    return;
                var client = _peerServices.GetClient("server", _nodeSink.MessageTag, "localhost", 8080);
                client.Send(_nodeSink.ToMessage(command.AsUTF8Bytes(), isLast: true), _nodeSink);
            }
        }
    }
}