/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;

namespace InterlockLedger.Peer2Peer
{
    public interface IClient
    {
        void Send(Span<byte> bytes, IMessageProcessor messageProcessor);
    }
}