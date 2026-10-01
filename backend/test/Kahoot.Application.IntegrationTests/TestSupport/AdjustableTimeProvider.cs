namespace Kahoot.Application.IntegrationTests.TestSupport;

public sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public AdjustableTimeProvider(DateTimeOffset initialUtcNow)
    {
        _utcNow = initialUtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public void Advance(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }
}
