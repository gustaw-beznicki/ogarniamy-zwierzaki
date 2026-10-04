using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ogarniamy_zwierzaki_api.Tests;

// A logger provider that keeps every entry the host's log filters let through, so a test can check what reaches the
// logs. Each entry is the level, the formatted message and the exception's full text.
public sealed class CapturedLogs : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => this;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue($"{logLevel}: {formatter(state, exception)}\n{exception}");

    public void Dispose()
    {
    }
}
