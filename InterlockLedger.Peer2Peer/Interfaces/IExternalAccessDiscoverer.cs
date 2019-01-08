/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IExternalAccessDiscoverer : IDisposable
    {
        Task<ExternalAccess> DetermineExternalAccessAsync(INodeSink nodeSink);
    }

    public class ExternalAccess
    {
        public ExternalAccess(string internalAddress, int internalPort, string externalAddress, int externalPort, Socket socket) {
            InternalAddress = internalAddress ?? throw new ArgumentNullException(nameof(internalAddress));
            InternalPort = internalPort;
            ExternalAddress = externalAddress ?? throw new ArgumentNullException(nameof(externalAddress));
            ExternalPort = externalPort;
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        public string ExternalAddress { get; }
        public int ExternalPort { get; }
        public string InternalAddress { get; }
        public int InternalPort { get; }
        public string Route => $"{InternalAddress}:{InternalPort} via {ExternalAddress}:{ExternalPort}!";
        public Socket Socket { get; }
    }
}