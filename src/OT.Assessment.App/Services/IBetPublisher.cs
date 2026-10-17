using OT.Assessment.Common.msg;

namespace OT.Assessment.App.Services
{
    public interface IBetPublisher
    {
        Task PublishAsync(CasinoBetMesg bet, CancellationToken cancellationToken = default);
    }
}

