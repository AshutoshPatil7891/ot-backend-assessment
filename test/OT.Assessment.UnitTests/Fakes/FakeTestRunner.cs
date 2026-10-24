using OT.Assessment.App.Models;
using OT.Assessment.App.Services;

namespace OT.Assessment.UnitTests.Fakes
{
    public class FakeTestRunner : ITestRunner
    {
        public bool IsRunning { get; set; }
        public string LastKind { get; private set; }
        public int CallCount { get; private set; }

        public Task<TestRunResult> RunAsync(string kind, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastKind = kind;
            return Task.FromResult(new TestRunResult { Kind = kind, Passed = true, ExitCode = 0, Output = "ok" });
        }
    }
}
