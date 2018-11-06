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
        void Send(IList<ArraySegment<byte>> segments, IClientSink messageProcessor);

        void Send(IList<ArraySegment<byte>> segments, IClientSink messageProcessor, Socket sender, bool waitForever);
    }
}