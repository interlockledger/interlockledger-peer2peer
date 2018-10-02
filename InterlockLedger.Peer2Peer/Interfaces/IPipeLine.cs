/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IPipeLine
    {
        bool KeepGoing { get; }
        bool ReadNoMore { get; }
        int RejectionReason { get; }
        bool WriteNoMore { get; }

        Task CompleteAsync();

        Task<byte[]> ReadBytesAsync(int count);

        void RecognizedFeature(string featureName, string messageName = null, string details = null);

        Task RejectAsync(int reason);

        Task<int> WriteBytesAsync(params byte[] bytes);

        Task<int> WriteBytesAsync(byte[] bytes, int offset, int count);
    }
}