/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IExternalAccessDiscoverer : IDisposable
    {
        Task<ExternalAccess> DetermineExternalAccessAsync(INodeSink nodeSink);
    }
}