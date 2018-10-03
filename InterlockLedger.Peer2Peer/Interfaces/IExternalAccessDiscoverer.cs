/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IExternalAccessDiscoverer
    {
        Task<(string address, int port, TcpListener listener)> DetermineExternalAccessAsync(INodeSink nodeSink);
    }
}