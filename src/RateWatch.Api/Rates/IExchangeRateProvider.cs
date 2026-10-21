namespace RateWatch.Api.Rates;

/// <summary>
/// Source of current exchange rates. Represents the external currency-rate API.
/// </summary>
public interface IExchangeRateProvider
{
    /// <returns>The latest rates against <paramref name="baseCurrency"/>, or null if the base currency is not supported.</returns>
    Task<RateSnapshot?> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default);
}
