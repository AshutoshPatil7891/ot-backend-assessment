using OT.Assessment.App.Models;

namespace OT.Assessment.App.Services
{
    public interface ITestRunner
    {
        /// <summary>True while a test run is in progress (only one run at a time).</summary>
        bool IsRunning { get; }

        /// <summary>Runs "unit" (the unit tests) or "load" (the Tester project) and returns its output.</summary>
        Task<TestRunResult> RunAsync(string kind, CancellationToken cancellationToken = default);
    }
}
