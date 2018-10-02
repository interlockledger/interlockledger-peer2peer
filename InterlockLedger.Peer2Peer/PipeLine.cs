/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    internal enum PipeLineState
    {
        Open = 0,
        Rejected = 1,
        Completed = 2
    }

    internal class PipeLine : IPipeLine
    {
        public PipeLine(NetworkStream stream) => _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        public bool KeepGoing => _state == PipeLineState.Open;
        public bool ReadNoMore => _state != PipeLineState.Open || !_stream.CanRead;
        public int RejectionReason { get; private set; }
        public bool WriteNoMore => _state != PipeLineState.Open || !_stream.CanWrite;

        public async Task CompleteAsync() {
            _state = PipeLineState.Completed;
            await _stream.FlushAsync();
            _stream.Close();
        }

        public async Task<byte[]> ReadBytesAsync(int count) {
            if (_stream.DataAvailable && !ReadNoMore) {
                var bytes = new byte[count];
                int bytesRead = await _stream.ReadAsync(bytes, 0, count);
                Array.Resize(ref bytes, bytesRead);
                return bytes;
            } else
                return Array.Empty<byte>();
        }

        public void RecognizedFeature(string featureName, string messageName = null, string details = null) {
            // Nothing to do for now
        }

        public async Task RejectAsync(int reason) {
            RejectionReason = reason;
            _state = PipeLineState.Rejected;
            await _stream.FlushAsync();
            _stream.Close();
        }

        public async Task<int> WriteBytesAsync(params byte[] bytes) => await WriteBytesAsync(bytes, 0, bytes?.Length ?? 0);

        public async Task<int> WriteBytesAsync(byte[] bytes, int offset, int count) {
            if (bytes == null || bytes.Length < offset + count)
                return 0;
            await _stream.WriteAsync(bytes, offset, count);
            return count;
        }

        private readonly NetworkStream _stream;
        private PipeLineState _state = PipeLineState.Open;
    }
}