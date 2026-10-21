using OT.Assessment.Common.Messages;

namespace OT.Assessment.App.Services
{
    public interface IBetPublisher
    {
        Task PublishAsync(CasinoBetMessage bet, CancellationToken cancellationToken = default);
    }
}

