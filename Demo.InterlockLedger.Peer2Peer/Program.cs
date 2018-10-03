using InterlockLedger.Peer2Peer;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Demo.InterlockLedger.Peer2Peer
{
    public static class Program
    {
        public static void Main(string[] args) {
            Console.WriteLine("Demo.InterlockLedger.Peer2Peer!");
            if (args.Length > 0 && args[0].Equals("client", StringComparison.OrdinalIgnoreCase))
                Client();
            else
                Task.WaitAll(ServerAsync());
        }

        public static async Task ServerAsync() {
            Console.WriteLine("Server");
            var factory = new LoggerFactory();
            factory.AddConsole(LogLevel.Information);
            var peerServices = new PeerServices(factory, new DummyExternalAccessDiscoverer(factory));
            using (var listener = peerServices.CreateFor(new DemoNodeSink())) {
                await listener.StartAsync();
                while (listener.Alive)
                    await Task.Yield();
                await listener.StopAsync();
            }
        }

        private static void Client() {
            Console.WriteLine("Client");
            while (true) {
                Console.Write("Command (x to exit, w to get who is answering, e... to echo ...): ");
                var command = Console.ReadLine();
                if (command.FirstOrDefault() == 'x')
                    return;
                Console.WriteLine(SendCommand(command));
            }
        }

        private static string SendCommand(string command) {
            using (var client = new TcpClient("localhost", 8080)) {
                using (NetworkStream stream = client.GetStream()) {
                    stream.Write(Encoding.UTF8.GetBytes(command).AsSpan());
                    stream.Flush();
                    return new StreamReader(stream, Encoding.UTF8).ReadLine();
                }
            }
        }
    }
}