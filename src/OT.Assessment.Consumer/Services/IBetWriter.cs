
using OT.Assessment.Common.msg;

namespace OT.Assessment.Consumer.Services
{
    public interface IBetWriter
    {
        Task WriteBatchAsync(
            IReadOnlyList<CasinoBetMesg> bets,
            CancellationToken cancellationToken = default);
    }
}

