using OT.Assessment.App.Services;
using OT.Assessment.Common.Messages;

namespace OT.Assessment.UnitTests.Fakes
{
    /// <summary>
    /// A stand-in for the RabbitMQ publisher. It records what was published, or throws
    /// the exception the test asks for, so the controller can be tested without RabbitMQ.
    /// </summary>
    public class FakeBetPublisher : IBetPublisher
    {
        public List<CasinoBetMessage> Published { get; } = new List<CasinoBetMessage>();

        public Exception ExceptionToThrow { get; set; }

        public Task PublishAsync(CasinoBetMessage bet, CancellationToken cancellationToken = default)
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
