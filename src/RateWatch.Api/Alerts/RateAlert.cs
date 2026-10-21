namespace RateWatch.Api.Alerts;

/// <summary>
/// An above-threshold, one-shot alert: it triggers once when the current rate is greater than <see cref="TargetRate"/>.
/// </summary>
/// <remarks>
/// Evaluation is planned and not implemented yet (see NOTES.md).
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

    private static string NormalizeCurrency(string currency, string paramName)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"Currency must be a 3-letter code, got '{currency}'.", paramName);
        }

        return currency.ToUpperInvariant();
    }
}
