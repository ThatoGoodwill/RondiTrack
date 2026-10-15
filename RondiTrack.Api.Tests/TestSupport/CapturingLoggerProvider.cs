using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RondiTrack.Api.Tests.TestSupport;

// One log entry, kept as everything a person with access to the logs could read.
public sealed record CapturedLog(
    string Category, LogLevel Level, string Message, string? Exception, IReadOnlyList<string> Properties)
{
    // The whole entry as ONE string: the rendered message, the exception (type, message and stack trace) and every
    // structured property. A test searches this, so a secret hiding in a property or an exception message is still found.
    public string AllText => string.Join('\n',
        new[] { Message, Exception }.Where(x => x is not null).Concat(Properties));
}

// A logger provider that remembers what it is given. The factory registers one, so a test can read back every
// line the app logged. It sees the same lines the console would: the app's own log levels still apply.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = new List<string>();
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                foreach (var pair in pairs)
                    properties.Add($"{pair.Key}={pair.Value}");

            sink.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception?.ToString(), properties));
        }
    }
}
