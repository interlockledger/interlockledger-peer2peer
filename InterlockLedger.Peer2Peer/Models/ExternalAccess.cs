/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Net.Sockets;

namespace InterlockLedger.Peer2Peer
{
    public class ExternalAccess
    {
        public ExternalAccess(Socket socket, string internalAddress, ushort internalPort, string externalAddress = null, ushort? externalPort = null) {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            InternalAddress = internalAddress ?? throw new ArgumentNullException(nameof(internalAddress));
            InternalPort = internalPort;
            ExternalAddress = externalAddress ?? internalAddress;
            ExternalPort = externalPort ?? internalPort;
        }

        public string ExternalAddress { get; }
        public ushort ExternalPort { get; }
        public string InternalAddress { get; }
        public ushort InternalPort { get; }
        public string Route => $"{InternalAddress}:{InternalPort} via {ExternalAddress}:{ExternalPort}!";
        public Socket Socket { get; }
    }
}