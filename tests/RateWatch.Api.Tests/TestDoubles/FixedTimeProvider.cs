namespace RateWatch.Api.Tests.TestDoubles;

public class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
