namespace RateWatch.Api.Tests;

/// <summary>Alert JSON returned by the alert endpoints, as seen by HTTP tests.</summary>
internal record AlertResponse(
    int Id, string BaseCurrency, string QuoteCurrency, decimal TargetRate,
    DateTimeOffset CreatedAt, DateTimeOffset? TriggeredAt);
