using OT.Assessment.Common.msg;

namespace OT.Assessment.App.Services
{
    public interface IWagerPublisher
    {
        Task PublishAsync(CasinoWagerMesg wager, CancellationToken cancellationToken = default);
    }
}

