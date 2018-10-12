/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IListener : IDisposable
    {
        bool Alive { get; }

        void Start();

        void Stop();
    }
}