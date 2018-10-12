/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public class DefaultMessageProcessor : IMessageProcessor
    {
        public DefaultMessageProcessor(Socket socket, Func<IEnumerable<ReadOnlyMemory<byte>>, Task<ReadOnlyMemory<byte>>> process) {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public void Process(List<ReadOnlyMemory<byte>> segments) {
            var result = _process(segments).Result;
            if (!result.IsEmpty)
                _socket.Send(result.ToArray(), SocketFlags.None);
        }

        private readonly Func<IEnumerable<ReadOnlyMemory<byte>>, Task<ReadOnlyMemory<byte>>> _process;
        private readonly Socket _socket;
    }
}
