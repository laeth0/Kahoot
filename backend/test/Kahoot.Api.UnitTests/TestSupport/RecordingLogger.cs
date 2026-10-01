namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();

    public IReadOnlyList<RecordedLogEntry> Entries => _entries.ToArray();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Dictionary<string, object?> structuredValues = new(StringComparer.OrdinalIgnoreCase);

        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (KeyValuePair<string, object?> pair in pairs)
            {
                structuredValues[pair.Key] = pair.Value;
            }
        }

        string formattedMessage = formatter(state, exception);
        _entries.Enqueue(new RecordedLogEntry(logLevel, eventId, exception, formattedMessage, structuredValues));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public sealed record RecordedLogEntry(
    LogLevel LogLevel,
    EventId EventId,
    Exception? Exception,
    string FormattedMessage,
    IReadOnlyDictionary<string, object?> StructuredValues)
{
    public object? GetValue(string key)
    {
        return StructuredValues.TryGetValue(key, out object? value) ? value : null;
    }
}
