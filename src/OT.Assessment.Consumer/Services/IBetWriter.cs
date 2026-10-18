
using OT.Assessment.Common.msg;

namespace OT.Assessment.Consumer.Services
{
    public interface IBetWriter
    {
        /// <summary>
        /// Saves the bets and returns how many new rows were added. A bet that is already stored
        /// (or repeated inside the batch) is skipped, so the number can be lower than bets.Count.
        /// </summary>
        Task<int> WriteBatchAsync(
            IReadOnlyList<CasinoBetMesg> bets,
            CancellationToken cancellationToken = default);
    }
}

