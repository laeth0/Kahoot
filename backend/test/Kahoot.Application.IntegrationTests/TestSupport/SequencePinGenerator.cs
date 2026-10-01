namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;

public sealed class SequencePinGenerator : IPinGeneratorService
{
    private readonly ConcurrentQueue<string> _pins;
    private int _callCount;

    public SequencePinGenerator(IEnumerable<string> pins)
    {
        _pins = new ConcurrentQueue<string>(pins);
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public string GeneratePin()
    {
        Interlocked.Increment(ref _callCount);
        if (_pins.TryDequeue(out string? pin))
        {
            return pin;
        }

        throw new InvalidOperationException("SequencePinGenerator exhausted configured PINs.");
    }
}
