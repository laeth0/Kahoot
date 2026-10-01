namespace Kahoot.Application.IntegrationTests.TestSupport;

using System;

public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset initialTime)
    {
        _utcNow = initialTime;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset time) => _utcNow = time;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
