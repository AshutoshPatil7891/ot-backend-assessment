using OT.Assessment.App.Services;
using OT.Assessment.Common.msg;

namespace OT.Assessment.UnitTests.Fakes
{
    /// <summary>
    /// A stand-in for the RabbitMQ publisher. It records what was published, or throws
    /// the exception the test asks for, so the controller can be tested without RabbitMQ.
    /// </summary>
    public class FakeBetPublisher : IBetPublisher
    {
        public List<CasinoBetMesg> Published { get; } = new List<CasinoBetMesg>();

        public Exception ExceptionToThrow { get; set; }

        public Task PublishAsync(CasinoBetMesg bet, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            Published.Add(bet);
            return Task.CompletedTask;
        }
    }
}
