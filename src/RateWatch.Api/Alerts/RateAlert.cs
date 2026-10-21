namespace RateWatch.Api.Alerts;

/// <summary>
/// An above-threshold, one-shot alert: it triggers once when the current rate is greater than <see cref="TargetRate"/>.
/// </summary>
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
        BaseCurrency = NormalizeCurrency(baseCurrency, nameof(baseCurrency));
        QuoteCurrency = NormalizeCurrency(quoteCurrency, nameof(quoteCurrency));
        if (QuoteCurrency == BaseCurrency)
        {
            throw new ArgumentException("Quote currency must differ from base currency.", nameof(quoteCurrency));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetRate);

        TargetRate = targetRate;
        CreatedAt = createdAt.ToUniversalTime();
    }

    /// <summary>
    /// Triggers the alert if it is still active and <paramref name="currentRate"/> is strictly greater than
    /// <see cref="TargetRate"/>. One-shot: once triggered, it never triggers again.
    /// </summary>
    /// <returns>True only if this call triggered the alert.</returns>
    public bool Trigger(decimal currentRate, DateTimeOffset now)
    {
        if (TriggeredAt is not null || currentRate <= TargetRate)
        {
            return false;
        }

        TriggeredAt = now.ToUniversalTime();
        return true;
    }

    private static string NormalizeCurrency(string currency, string paramName)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"Currency must be a 3-letter code, got '{currency}'.", paramName);
        }

        return currency.ToUpperInvariant();
    }
}
