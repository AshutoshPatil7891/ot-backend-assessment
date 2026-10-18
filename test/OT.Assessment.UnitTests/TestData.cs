using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OT.Assessment.App.Controllers;
using OT.Assessment.Common.msg;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Small helpers shared by the tests.</summary>
    public static class TestData
    {
        public static PlayerController CreateController(
            FakeBetPublisher publisher, FakePlayerReader reader, ILogger<PlayerController> logger = null)
        {
            return new PlayerController(publisher, reader, logger ?? NullLogger<PlayerController>.Instance);
        }

        public static CasinoBetMesg ValidBet()
        {
            return new CasinoBetMesg
            {
                BetId = Guid.Parse("aa6700eb-1a06-483e-9739-d293dc7a9383"),
                AccountId = Guid.Parse("5ac75fec-23e9-27d1-b660-179eee70003d"),
                Username = "Jay.Bernhard67",
                CountryCode = "BS",
                GameName = "Ergonomic Granite Cheese",
                Provider = "Ergonomic Soft Fish",
                Theme = "adventure",
                Amount = 500.25m,
                CreatedDateTime = DateTimeOffset.Parse("2024-05-04T02:25:05.9906387+02:00"),
                NumberOfBets = 3,
                Duration = 1827254,
                TransactionId = Guid.Parse("410b7161-3473-4d74-85c3-a533d050a9d3")
            };
        }
    }
}
