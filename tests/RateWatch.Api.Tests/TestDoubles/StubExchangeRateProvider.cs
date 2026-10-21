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

    public Task<RateSnapshot?> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default) =>
        Task.FromResult(_snapshots.GetValueOrDefault(baseCurrency));
}
