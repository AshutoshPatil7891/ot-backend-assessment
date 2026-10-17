using OT.Assessment.Common.msg;
using OT.Assessment.Common.Resp;
using System.Text.Json;

namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Tests for the JSON shapes: what the Tester sends, what goes through the queue,
    /// and what the GET endpoints return.
    /// </summary>
    public class MessageContractTests
    {
        // ASP.NET Core reads and writes JSON with these defaults (camelCase, case-insensitive).
        private static readonly JsonSerializerOptions Web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private const string TesterJson = @"{
            ""wagerId"": ""aa6700eb-1a06-483e-9739-d293dc7a9383"",
            ""theme"": ""adventure"",
            ""provider"": ""Ergonomic Soft Fish"",
            ""gameName"": ""Ergonomic Granite Cheese"",
            ""transactionId"": ""410b7161-3473-4d74-85c3-a533d050a9d3"",
            ""brandId"": ""8a2016f8-c4c4-471f-9a9c-337a54664650"",
            ""accountId"": ""5ac75fec-23e9-27d1-b660-179eee70003d"",
            ""Username"": ""Jay.Bernhard67"",
            ""externalReferenceId"": ""0267dbca-2760-4a9e-ab42-5ce766fa8ca0"",
            ""transactionTypeId"": ""8aaece0c-5d53-4225-a937-adb454c4da31"",
            ""amount"": 38273.974454660885,
            ""createdDateTime"": ""2024-05-04T02:25:05.9906387+02:00"",
            ""numberOfBets"": 3,
            ""countryCode"": ""BS"",
            ""sessionData"": ""some text the database does not store"",
            ""Duration"": 1827254
        }";

        // ---------- positive scenarios ----------

        [Fact]
        public void TesterJson_BindsToTheMessage_AndExtraFieldsAreIgnored()
        {
            var bet = JsonSerializer.Deserialize<CasinoBetMesg>(TesterJson, Web);

            Assert.Equal(Guid.Parse("aa6700eb-1a06-483e-9739-d293dc7a9383"), bet.BetId);
            Assert.Equal(Guid.Parse("5ac75fec-23e9-27d1-b660-179eee70003d"), bet.AccountId);
            Assert.Equal("Jay.Bernhard67", bet.Username);
            Assert.Equal("Ergonomic Granite Cheese", bet.GameName);
            Assert.Equal("Ergonomic Soft Fish", bet.Provider);
            Assert.Equal("adventure", bet.Theme);
            Assert.Equal("BS", bet.CountryCode);
            Assert.Equal(3, bet.NumberOfBets);
            Assert.Equal(1827254L, bet.Duration);
            Assert.Equal(38273.974454660885m, bet.Amount);
        }

        [Fact]
        public void TesterJson_KeepsTheTimeZoneOffset()
        {
            var bet = JsonSerializer.Deserialize<CasinoBetMesg>(TesterJson, Web);

            Assert.Equal(TimeSpan.FromHours(2), bet.CreatedDateTime.Offset);
        }

        [Fact]
        public void BetSentThroughTheQueue_ComesBackUnchanged()
        {
            var original = TestData.ValidBet();

            // The publisher and the consumer both use the default options.
            var bytes = JsonSerializer.SerializeToUtf8Bytes(original);
            var copy = JsonSerializer.Deserialize<CasinoBetMesg>(bytes);

            Assert.Equal(original.BetId, copy.BetId);
            Assert.Equal(original.AccountId, copy.AccountId);
            Assert.Equal(original.Username, copy.Username);
            Assert.Equal(original.Amount, copy.Amount);
            Assert.Equal(original.CreatedDateTime, copy.CreatedDateTime);
            Assert.Equal(original.CreatedDateTime.Offset, copy.CreatedDateTime.Offset);
            Assert.Equal(original.TransactionId, copy.TransactionId);
        }

        [Fact]
        public void QueueMessage_UsesTheNameWagerIdForTheBetId()
        {
            var json = JsonSerializer.Serialize(TestData.ValidBet());

            Assert.Contains("\"wagerId\"", json);
            Assert.DoesNotContain("\"BetId\"", json);
        }

        [Fact]
        public void PagedResponse_UsesTheFieldNamesFromTheBrief()
        {
            var page = new PagedResp<PlayerBetResp> { Page = 1, PageSize = 10, Total = 35, TotalPages = 4 };

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(page, Web));
            var root = doc.RootElement;

            Assert.Equal(JsonValueKind.Array, root.GetProperty("data").ValueKind);
            Assert.Equal(1, root.GetProperty("page").GetInt32());
            Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
            Assert.Equal(35, root.GetProperty("total").GetInt32());
            Assert.Equal(4, root.GetProperty("totalPages").GetInt32());
        }

        [Fact]
        public void PlayerBetRow_UsesTheFieldNamesFromTheBrief()
        {
            var row = new PlayerBetResp { Game = "Game X", Provider = "Prov 1", Amount = 500m, CreatedDate = DateTimeOffset.UtcNow };

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(row, Web));
            var root = doc.RootElement;

            Assert.Equal("Game X", root.GetProperty("game").GetString());
            Assert.Equal("Prov 1", root.GetProperty("provider").GetString());
            Assert.Equal(500m, root.GetProperty("amount").GetDecimal());
            Assert.True(root.TryGetProperty("createdDate", out _));
        }

        [Fact]
        public void TopSpenderRow_UsesTheFieldNamesFromTheBrief()
        {
            var row = new TopSpenderResp { AccountId = Guid.NewGuid(), Username = "testUser1", TotalAmountSpend = 150m };

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(row, Web));
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("accountId", out _));
            Assert.Equal("testUser1", root.GetProperty("username").GetString());
            Assert.Equal(150m, root.GetProperty("totalAmountSpend").GetDecimal());
        }

        // ---------- negative scenarios ----------

        [Fact]
        public void MalformedGuid_IsRejectedWhenReadingTheBody()
        {
            var json = TesterJson.Replace("5ac75fec-23e9-27d1-b660-179eee70003d", "not-a-guid");

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CasinoBetMesg>(json, Web));
        }

        [Fact]
        public void TextInsteadOfANumber_IsRejectedWhenReadingTheBody()
        {
            var json = TesterJson.Replace("38273.974454660885", "\"lots\"");

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CasinoBetMesg>(json, Web));
        }

        [Fact]
        public void BrokenJson_IsRejected()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CasinoBetMesg>("{ this is not json", Web));
        }

        [Fact]
        public void MissingFields_LeaveNullsAndZeros_WhichTheControllerThenRejects()
        {
            var bet = JsonSerializer.Deserialize<CasinoBetMesg>("{}", Web);

            Assert.Equal(Guid.Empty, bet.AccountId);
            Assert.Null(bet.Username);
            Assert.Equal(0m, bet.Amount);
        }
    }
}
