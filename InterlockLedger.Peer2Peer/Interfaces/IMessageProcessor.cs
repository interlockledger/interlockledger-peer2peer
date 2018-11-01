using System;
using System.Collections.Generic;

namespace InterlockLedger.Peer2Peer
{
    [Flags]
    public enum Success
    {
        None = 0,
        Retry = 1,
        Processed = 2,
        SwitchToListen = 4,
        Exit = 128
    }

    public interface IMessageProcessor
    {
        bool AwaitMultipleAnswers { get; }

        Success Process(List<ReadOnlyMemory<byte>> segments);
    }
}