using OT.Assessment.Common.Logging;
using Serilog;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for the daily log files written by FileLogging (info, warning and error in separate folders).</summary>
    public class FileLoggingTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "ot-logs-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Fact]
        public void EachLevelGoesToItsOwnFolder_AndOnlyThere()
        {
            WriteOneOfEach();

            var info = ReadFolder("info");
            var warning = ReadFolder("warning");
            var error = ReadFolder("error");

            Assert.Contains("an info message", info);
            Assert.DoesNotContain("a warning message", info);
            Assert.DoesNotContain("an error message", info);

            Assert.Contains("a warning message", warning);
            Assert.DoesNotContain("an info message", warning);
            Assert.DoesNotContain("an error message", warning);

            Assert.Contains("an error message", error);
            Assert.DoesNotContain("an info message", error);
            Assert.DoesNotContain("a warning message", error);
        }

        [Fact]
        public void FatalMessagesAreWrittenToTheErrorFolder()
        {
            var logger = FileLogging.Configure(new LoggerConfiguration(), _folder).CreateLogger();
            logger.Fatal("a fatal message");
            ((IDisposable)logger).Dispose();

            Assert.Contains("a fatal message", ReadFolder("error"));
        }

        [Fact]
        public void FileNameContainsTheDate_SoEachDayGetsItsOwnFile()
        {
            WriteOneOfEach();

            var expected = "info-" + DateTime.Now.ToString("yyyyMMdd") + ".log";
            Assert.True(File.Exists(Path.Combine(_folder, "info", expected)), "Missing " + expected);
        }

        [Fact]
        public void ExceptionDetailsAreWrittenToTheErrorFile()
        {
            var logger = FileLogging.Configure(new LoggerConfiguration(), _folder).CreateLogger();
            logger.Error(new InvalidOperationException("boom detail"), "an error message");
            ((IDisposable)logger).Dispose();

            Assert.Contains("boom detail", ReadFolder("error"));
        }

        [Fact]
        public void MicrosoftFrameworkInformationNoiseIsNotWritten()
        {
            var logger = FileLogging.Configure(new LoggerConfiguration(), _folder).CreateLogger();
            logger.ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics").Information("request noise");
            logger.Information("our own message");
            ((IDisposable)logger).Dispose();

            var info = ReadFolder("info");
            Assert.DoesNotContain("request noise", info);
            Assert.Contains("our own message", info);
        }

        private void WriteOneOfEach()
        {
            var logger = FileLogging.Configure(new LoggerConfiguration(), _folder).CreateLogger();
            logger.Information("an info message");
            logger.Warning("a warning message");
            logger.Error("an error message");
            ((IDisposable)logger).Dispose();
        }

        private string ReadFolder(string name)
        {
            var path = Path.Combine(_folder, name);
            if (!Directory.Exists(path))
            {
                return string.Empty;
            }
            return string.Concat(Directory.GetFiles(path).Select(File.ReadAllText));
        }
    }
}
