/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IClient
    {
        Task SendAsync(IList<ArraySegment<byte>> segments, IClientSink clientSink);

        Task SendAsync(IList<ArraySegment<byte>> segments, IClientSink clientSink, Socket sender);

        void Send(IList<ArraySegment<byte>> segments, IClientSink clientSink);

        void Send(IList<ArraySegment<byte>> segments, IClientSink clientSink, Socket sender);
    }
}