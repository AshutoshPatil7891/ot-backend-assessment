using Microsoft.Extensions.Logging;

namespace OT.Assessment.UnitTests.Fakes
{
    /// <summary>
    /// A stand-in logger that remembers every message (with its level), so tests can check
    /// that the controller logs what it should.
    /// </summary>
    public class FakeLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        public IEnumerable<string> Messages(LogLevel level)
        {
            return Entries.Where(e => e.Level == level).Select(e => e.Message);
        }
    }
}
