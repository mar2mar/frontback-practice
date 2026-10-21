using RateWatch.Api.Rates;
using RateWatch.Api.Tests.TestDoubles;

namespace RateWatch.Api.Tests;

public class FakeExchangeRateProviderTests
{
    private const decimal Volatility = 0.005m;
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static FakeExchangeRateProvider CreateProvider(decimal volatility = Volatility) =>
        new(new FixedTimeProvider(Now), new Random(42), volatility);

    [Fact]
    public async Task Returns_every_supported_currency_except_the_base()
    {
        var snapshot = await CreateProvider().GetLatestRatesAsync("EUR");

        var expected = FakeExchangeRateProvider.UsdMidMarketRates.Keys.Where(c => c != "EUR").Order();
        Assert.Equal(expected, snapshot!.Rates.Select(r => r.Currency).Order());
    }

    [Fact]
    public async Task Usd_rates_stay_within_volatility_of_mid_market_rates()
    {
        var provider = CreateProvider();

        for (var i = 0; i < 1000; i++)
        {
            var snapshot = await provider.GetLatestRatesAsync("USD");

            foreach (var rate in snapshot!.Rates)
            {
                var midMarket = FakeExchangeRateProvider.UsdMidMarketRates[rate.Currency];
                var maxDeviation = midMarket * Volatility + 0.000001m; // allow for rounding to 6 dp
                Assert.InRange(rate.Rate, midMarket - maxDeviation, midMarket + maxDeviation);
            }
        }
    }

    [Fact]
    public async Task Cross_rates_are_derived_from_usd_rates()
    {
        var snapshot = await CreateProvider(volatility: 0m).GetLatestRatesAsync("EUR");

        var rates = snapshot!.Rates.ToDictionary(r => r.Currency, r => r.Rate);
        Assert.Equal(Math.Round(0.79m / 0.92m, 6), rates["GBP"]);
        Assert.Equal(Math.Round(1m / 0.92m, 6), rates["USD"]);
    }

    [Fact]
    public async Task Successive_calls_return_fresh_rates()
    {
        var provider = CreateProvider();

        var first = await provider.GetLatestRatesAsync("USD");
        var second = await provider.GetLatestRatesAsync("USD");

        Assert.NotEqual(first!.Rates.Select(r => r.Rate), second!.Rates.Select(r => r.Rate));
    }

    [Fact]
    public async Task Timestamp_comes_from_the_time_provider()
    {
        var snapshot = await CreateProvider().GetLatestRatesAsync("USD");

        Assert.Equal(Now, snapshot!.Timestamp);
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("Usd")]
    public async Task Base_currency_is_case_insensitive(string baseCurrency)
    {
        var snapshot = await CreateProvider().GetLatestRatesAsync(baseCurrency);

        Assert.Equal("USD", snapshot!.BaseCurrency);
    }

    [Fact]
    public async Task Unsupported_base_currency_returns_null()
    {
        var snapshot = await CreateProvider().GetLatestRatesAsync("XYZ");

        Assert.Null(snapshot);
    }
}
