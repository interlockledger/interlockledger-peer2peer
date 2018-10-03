/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    // ? Tweak this to use the new System.IO.Pipelines https://blogs.msdn.microsoft.com/dotnet/2018/07/09/system-io-pipelines-high-performance-io-in-net/ ?
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