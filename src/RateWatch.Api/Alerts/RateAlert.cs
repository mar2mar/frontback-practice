namespace RateWatch.Api.Alerts;

/// <summary>
/// An above-threshold, one-shot alert: it triggers once when the current rate is greater than <see cref="TargetRate"/>.
/// </summary>
/// <remarks>
/// Constructor validation and evaluation are planned and not implemented yet (see NOTES.md).
/// </remarks>
public class RateAlert
{
    public int Id { get; private set; }
    public string BaseCurrency { get; private set; }
    public string QuoteCurrency { get; private set; }
    public decimal TargetRate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? TriggeredAt { get; private set; }

    public RateAlert(string baseCurrency, string quoteCurrency, decimal targetRate, DateTimeOffset createdAt)
    {
        BaseCurrency = baseCurrency;
        QuoteCurrency = quoteCurrency;
        TargetRate = targetRate;
        CreatedAt = createdAt;
    }
}
