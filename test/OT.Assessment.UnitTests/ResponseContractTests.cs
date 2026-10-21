using System.Text.Json;
using OT.Assessment.Common.Responses;

namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Tests that the JSON returned by the GET endpoints uses exactly the field names shown in the
    /// brief (the API serialises with the web defaults, which are camelCase).
    /// </summary>
    public class ResponseContractTests
    {
        private static readonly JsonSerializerOptions Web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private static string[] PropertyNames(object value)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, Web));
            return doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        }

        [Fact]
        public void PlayerBet_IsReturnedWithTheFieldNamesFromTheBrief()
        {
            var names = PropertyNames(new PlayerBetResponse());

            Assert.Equal(new[] { "wagerId", "game", "provider", "amount", "createdDate" }, names);
        }

        [Fact]
        public void PlayerBet_KeepsTheBetIdValueUnderWagerId()
        {
            var id = Guid.Parse("aa6700eb-1a06-483e-9739-d293dc7a9383");

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new PlayerBetResponse { BetId = id }, Web));

            Assert.Equal(id.ToString(), doc.RootElement.GetProperty("wagerId").GetString());
        }

        [Fact]
        public void PagedResult_IsReturnedWithTheFieldNamesFromTheBrief()
        {
            var names = PropertyNames(new PagedResponse<PlayerBetResponse>());

            Assert.Equal(new[] { "data", "page", "pageSize", "total", "totalPages" }, names);
        }

        [Fact]
        public void TopSpender_IsReturnedWithTheFieldNamesFromTheBrief()
        {
            var names = PropertyNames(new TopSpenderResponse());

            Assert.Equal(new[] { "accountId", "username", "totalAmountSpend" }, names);
        }
    }
}
