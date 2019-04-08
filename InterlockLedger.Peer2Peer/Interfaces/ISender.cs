using System.Threading;
using System.Threading.Tasks;

namespace InterlockLedger.Peer2Peer
{
    public interface ISender
    {
        bool Exit { get; }

        void Send(Response response);

        void Stop();
    }
}