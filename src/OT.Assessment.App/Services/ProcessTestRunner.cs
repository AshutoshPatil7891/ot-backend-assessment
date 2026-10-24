using Microsoft.Extensions.Options;
using OT.Assessment.App.Models;
using OT.Assessment.App.Settings;
using System.Diagnostics;
using System.Text;

namespace OT.Assessment.App.Services
{
    /// <summary>
    /// Runs "dotnet test" (unit) or "dotnet run" on the Tester (load) as a child process and
    /// collects what it prints. Builds go to a separate folder, so they never touch the
    /// files of the API that is running.
    /// </summary>
    public class ProcessTestRunner : ITestRunner
    {
        // the last part of the output is returned, the start of a long run is not needed
        private const int MaxOutputChars = 8000;
        private static readonly TimeSpan MaxRunTime = TimeSpan.FromMinutes(10);

        private readonly string _solutionPath;
        private readonly ILogger<ProcessTestRunner> _logger;
        private int _running;

        public ProcessTestRunner(IOptions<AdminSettings> admin, IWebHostEnvironment environment, ILogger<ProcessTestRunner> logger)
        {
            _logger = logger;
            _solutionPath = string.IsNullOrWhiteSpace(admin.Value.SolutionPath)
                ? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."))
                : admin.Value.SolutionPath;
        }

        public bool IsRunning => Volatile.Read(ref _running) == 1;

        public async Task<TestRunResult> RunAsync(string kind, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                throw new InvalidOperationException("A test run is already in progress.");
            }

            try
            {
                var artifacts = Path.Combine(Path.GetTempPath(), "ot-assessment-admin-runs");
                var project = kind == "unit"
                    ? Path.Combine(_solutionPath, "test", "OT.Assessment.UnitTests")
                    : Path.Combine(_solutionPath, "test", "OT.Assessment.Tester");

                var info = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = _solutionPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (kind == "unit")
                {
                    info.ArgumentList.Add("test");
                    info.ArgumentList.Add(project);
                }
                else
                {
                    info.ArgumentList.Add("run");
                    info.ArgumentList.Add("--project");
                    info.ArgumentList.Add(project);
                    info.ArgumentList.Add("-c");
                    info.ArgumentList.Add("Release");
                }
                info.ArgumentList.Add("--artifacts-path");
                info.ArgumentList.Add(artifacts);

                return await RunProcessAsync(kind, info, cancellationToken);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }

        private async Task<TestRunResult> RunProcessAsync(string kind, ProcessStartInfo info, CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var timer = Stopwatch.StartNew();

            using var process = new Process { StartInfo = info };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(MaxRunTime);

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // the admin closed the page or the run took too long: stop the child process
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                _logger.LogWarning("The {Kind} test run was stopped", kind);
                throw;
            }

            // lets the last output lines arrive before reading them
            process.WaitForExit();

            string text;
            lock (output) text = output.ToString();
            if (text.Length > MaxOutputChars) text = text.Substring(text.Length - MaxOutputChars);

            return new TestRunResult
            {
                Kind = kind,
                ExitCode = process.ExitCode,
                Passed = process.ExitCode == 0,
                Seconds = Math.Round(timer.Elapsed.TotalSeconds, 1),
                Output = text
            };
        }
    }
}
