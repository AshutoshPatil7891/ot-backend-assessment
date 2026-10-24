namespace OT.Assessment.App.Models
{
    /// <summary>The outcome of one test run started from the admin portal.</summary>
    public class TestRunResult
    {
        public string Kind { get; set; }
        public bool Passed { get; set; }
        public int ExitCode { get; set; }
        public double Seconds { get; set; }
        public string Output { get; set; }
    }
}
