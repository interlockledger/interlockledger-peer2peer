// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2022 InterlockLedger Network
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met
//
// * Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// * Neither the name of the copyright holder nor the names of its
//   contributors may be used to endorse or promote products derived from
//   this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES, LOSS OF USE, DATA, OR PROFITS, OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// ******************************************************************************************************************************

#nullable enable

using System.Net;

namespace InterlockLedger.Peer2Peer;

public class DummyExternalAccessDiscoverer : AbstractExternalAccessDiscoverer
{
    public DummyExternalAccessDiscoverer(SocketFactory socketFactory, ILoggerFactory loggerFactory) {
        _socketFactory = socketFactory.Required();
        _logger = loggerFactory.Required().CreateLogger<DummyExternalAccessDiscoverer>();
    }

    public override async Task<ExternalAccess> DetermineExternalAccessAsync(string hostAtAddress, ushort hostAtPortNumber, string publishAtAddress, ushort? publishAtPortNumber) {
        if (Disposed)
            return default;
        string hostingAddress = hostAtAddress ?? "localhost";
        try {
            await Task.Yield();
            var listenerSocket = _socketFactory.GetSocket(hostingAddress, hostAtPortNumber);
            var port = (ushort)((IPEndPoint)listenerSocket.Required().LocalEndPoint.Required()).Port;
            return new ExternalAccess(listenerSocket, hostingAddress, port, publishAtAddress, publishAtPortNumber);
        } catch (Exception se) {
            _logger.LogError(se, "Could not open a listening socket for {hostingAddress}:{hostAtPortNumber}", hostingAddress, hostAtPortNumber);
            throw new InterlockLedgerIOException($"Could not open a listening socket for {hostingAddress}:{hostAtPortNumber}", se);
        }
    }

    protected override void DisposeManagedResources() { }

    private readonly ILogger<DummyExternalAccessDiscoverer> _logger;
    private readonly SocketFactory _socketFactory;
}