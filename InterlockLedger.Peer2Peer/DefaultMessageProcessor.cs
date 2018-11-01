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
    public class DefaultMessageProcessor : IMessageProcessor
    {
        public DefaultMessageProcessor(Socket socket, Func<IEnumerable<ReadOnlyMemory<byte>>, Action<ReadOnlyMemory<byte>, bool>, Task> process) {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public Success Process(List<ReadOnlyMemory<byte>> segments) {
            Task.WaitAll(_process(segments, Respond));
            return Success.Processed;
        }

        private void Respond(ReadOnlyMemory<byte> result, bool quit) {
            if (!(quit || result.IsEmpty))
                _socket.Send(result.ToArray(), SocketFlags.None);
        }

        private readonly Func<IEnumerable<ReadOnlyMemory<byte>>, Action<ReadOnlyMemory<byte>, bool>, Task> _process;
        private readonly Socket _socket;

        public bool AwaitMultipleAnswers => false;
    }
}