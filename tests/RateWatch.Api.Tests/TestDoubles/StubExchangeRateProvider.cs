using RateWatch.Api.Rates;

namespace RateWatch.Api.Tests.TestDoubles;

/// <summary>
/// Deterministic provider that returns exactly the rates configured by the test.
/// Rates can be reconfigured between calls, e.g. to simulate a rate crossing a threshold.
/// </summary>
public class StubExchangeRateProvider : IExchangeRateProvider
{
    private readonly Dictionary<string, RateSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public void SetRates(string baseCurrency, DateTimeOffset timestamp, params ExchangeRate[] rates) =>
        _snapshots[baseCurrency] = new RateSnapshot(baseCurrency.ToUpperInvariant(), timestamp, rates);

    private readonly HashSet<string> _failingBases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Makes requests for this base currency throw, simulating a provider failure.</summary>
    public void ThrowFor(string baseCurrency) => _failingBases.Add(baseCurrency);

    /// <summary>Base currencies requested, in call order.</summary>
    public List<string> RequestedBases { get; } = [];

    public Task<RateSnapshot?> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default)
    {
        RequestedBases.Add(baseCurrency);
        if (_failingBases.Contains(baseCurrency))
        {
            throw new InvalidOperationException($"Rate provider failed for '{baseCurrency}'.");
        }

        return Task.FromResult(_snapshots.GetValueOrDefault(baseCurrency));
    }
}
