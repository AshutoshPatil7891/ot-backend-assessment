namespace OT.Assessment.App.Models
{
    /// <summary>One game the player can pick for a bet.</summary>
    public class GameResponse
    {
        public string GameName { get; set; }
        public string Provider { get; set; }
        public string Theme { get; set; }
    }
}
