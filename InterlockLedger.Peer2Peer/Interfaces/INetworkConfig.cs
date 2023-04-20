// ******************************************************************************************************************************
//  
// Copyright (c) 2018-2023 InterlockLedger Network
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

namespace InterlockLedger.Peer2Peer
{
    /// <summary>
    /// Basic Peer2Peer Network Configuration
    /// </summary>
    public interface INetworkConfig : IDisposable
    {
        /// <summary>
        /// No read or written bytes while this time elapses will cause the connection to be shut
        /// 0 - means no timeout
        /// </summary>
        int InactivityTimeoutInMinutes { get; }

        /// <summary>
        /// Default size in bytes for listening buffers
        /// </summary>
        int ListeningBufferSize { get; }

        /// <summary>
        /// Tag identifier for liveness messages, all liveness messages must begin with the value encoded as an ILInt
        /// Must be different from <see cref="MessageTag"/>
        /// </summary>
        ulong LivenessMessageTag { get; }

        /// <summary>
        /// Maximum number of concurrent connections
        /// </summary>
        int MaxConcurrentConnections { get; }

        /// <summary>
        /// Tag identifier for messages, all messages must begin with the value encoded as an ILInt
        /// </summary>
        ulong MessageTag { get; }
        /// <summary>
        /// Network name, multiple networks can coexist using the same protocol
        /// </summary>
        string NetworkName { get; }

        /// <summary>
        /// Protocol name for the network that Peer2Peer will interact
        /// </summary>
        string NetworkProtocolName { get; }
    }
}