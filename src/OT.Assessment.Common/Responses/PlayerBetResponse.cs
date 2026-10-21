using System.Text.Json.Serialization;

namespace OT.Assessment.Common.Responses

{
    public class PlayerBetResponse
    {
        [JsonPropertyName("wagerId")]
        public Guid BetId { get; set; }
        public string Game { get; set; }
        public string Provider { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}

