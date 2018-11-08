/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Collections.Generic;

namespace InterlockLedger.Peer2Peer
{
    public abstract class Responder
    {
        public void Respond(Response response) {
            if (!response.Exit) {
                SendResponse(response.DataList);
            }
        }

        protected abstract void SendResponse(IList<ArraySegment<byte>> responseSegments);
    }
}