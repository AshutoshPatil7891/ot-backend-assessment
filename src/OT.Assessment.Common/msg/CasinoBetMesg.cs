
using System.Text.Json.Serialization;

namespace OT.Assessment.Common.msg
{
    public class CasinoBetMesg
    {
        [JsonPropertyName("wagerId")]
        public Guid BetId { get; set; }        
        public Guid AccountId { get; set; }        
        public string Username { get; set; }        
        public string CountryCode { get; set; }        
        public string GameName { get; set; }        
        public string Provider { get; set; }        
        public string Theme { get; set; }       
        public decimal Amount { get; set; }       
        public DateTimeOffset CreatedDateTime { get; set; }        
        public int NumberOfBets { get; set; }        
        public long Duration { get; set; }       
        public Guid TransactionId { get; set; }
    }
}

