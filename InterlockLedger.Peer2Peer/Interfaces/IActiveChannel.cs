using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface IActiveChannel
    {
        bool Active { get; }
        ulong Channel { get; }
        IConnection Connection { get; }

        bool Send(byte[] message);

        Task<Success> SinkAsync(byte[] message);
    }
}