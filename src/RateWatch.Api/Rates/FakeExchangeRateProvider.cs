namespace RateWatch.Api.Rates;

/// <summary>
/// Simulates an external rate API. Each call returns USD rates that fluctuate randomly
/// within ±volatility of fixed mid-market rates; other bases are derived as cross rates.
/// </summary>
public class FakeExchangeRateProvider(TimeProvider timeProvider, Random random, decimal volatility = 0.005m)
    : IExchangeRateProvider
{
    public static readonly IReadOnlyDictionary<string, decimal> UsdMidMarketRates = new Dictionary<string, decimal>
    {
        ["USD"] = 1m,
        ["EUR"] = 0.92m,
        ["GBP"] = 0.79m,
        ["JPY"] = 149.50m,
        ["CAD"] = 1.37m,
        ["AUD"] = 1.52m,
        ["CHF"] = 0.88m,
    };

    public Task<RateSnapshot?> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default)
    {
        var baseCode = baseCurrency.ToUpperInvariant();
        if (!UsdMidMarketRates.ContainsKey(baseCode))
        {
            return Task.FromResult<RateSnapshot?>(null);
        }

        var usdRates = UsdMidMarketRates.ToDictionary(
            r => r.Key,
            r => r.Key == "USD" ? 1m : Fluctuate(r.Value));

        var rates = usdRates
            .Where(r => r.Key != baseCode)
            .Select(r => new ExchangeRate(r.Key, Math.Round(r.Value / usdRates[baseCode], 6)))
            .ToList();

        return Task.FromResult<RateSnapshot?>(new RateSnapshot(baseCode, timeProvider.GetUtcNow(), rates));
    }

    private decimal Fluctuate(decimal rate)
    {
        var change = (decimal)(random.NextDouble() * 2 - 1) * volatility;
        return rate * (1 + change);
    }
}
