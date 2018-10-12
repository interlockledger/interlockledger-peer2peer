using System;
using System.Collections.Generic;

namespace InterlockLedger.Peer2Peer
{
    public interface IMessageProcessor
    {
        void Process(List<ReadOnlyMemory<byte>> segments);
    }
}