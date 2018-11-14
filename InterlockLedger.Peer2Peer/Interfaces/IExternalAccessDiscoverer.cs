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
        Task<(string address, int port, Socket socket)> DetermineExternalAccessAsync(INodeSink nodeSink);
    }
}
