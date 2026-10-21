
using OT.Assessment.Common.Messages;

namespace OT.Assessment.Consumer.Services
{
    public interface IBetWriter
    {
        /// <summary>
        /// Saves the bets and returns how many new rows were added. A bet that is already stored
        /// (or repeated inside the batch) is skipped, so the number can be lower than bets.Count.
        /// </summary>
        Task<int> WriteBatchAsync(
            IReadOnlyList<CasinoBetMessage> bets,
            CancellationToken cancellationToken = default);
    }
}

