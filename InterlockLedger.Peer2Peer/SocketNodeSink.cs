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
    public class SocketResponder : Responder
    {
        public SocketResponder(Socket socket) => _socket = socket ?? throw new ArgumentNullException(nameof(socket));

        protected override void SendResponse(IList<ArraySegment<byte>> responseSegments) => _socket.Send(responseSegments);

        private readonly Socket _socket;
    }
}