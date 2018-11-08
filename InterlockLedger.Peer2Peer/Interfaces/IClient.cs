/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    public interface IClient
    {
        void Send(IList<ArraySegment<byte>> segments, IClientSink clientSink);

        void Send(IList<ArraySegment<byte>> segments, IClientSink clientSink, Socket sender);
    }
}