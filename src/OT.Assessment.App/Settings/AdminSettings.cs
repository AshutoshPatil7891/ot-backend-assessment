namespace OT.Assessment.App.Settings
{
    /// <summary>The admin login (local development only) and where the solution folder is.</summary>
    public class AdminSettings
    {
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";

        // folder that holds OT.Assessment.sln; empty = two folders above the API project
        public string SolutionPath { get; set; }
    }
}
