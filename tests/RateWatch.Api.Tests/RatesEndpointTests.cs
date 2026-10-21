using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RateWatch.Api.Rates;
using RateWatch.Api.Tests.TestDoubles;

namespace RateWatch.Api.Tests;

public class RatesEndpointTests(RateWatchApiFactory factory)
    : IClassFixture<RateWatchApiFactory>
{
    [Fact]
    public async Task Get_rates_defaults_to_usd_and_returns_provider_rates()
    {
        var timestamp = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var stub = new StubExchangeRateProvider();
        stub.SetRates("USD", timestamp, new ExchangeRate("EUR", 0.9m), new ExchangeRate("GBP", 0.8m));
        var client = factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider>(stub)))
            .CreateClient();

        var response = await client.GetAsync("/api/rates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<RateSnapshot>();
        Assert.Equal("USD", snapshot!.BaseCurrency);
        Assert.Equal(timestamp, snapshot.Timestamp);
        Assert.Equal([new ExchangeRate("EUR", 0.9m), new ExchangeRate("GBP", 0.8m)], snapshot.Rates);
    }

    [Fact]
    public async Task Get_rates_with_base_returns_rates_against_that_base()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/rates?base=eur");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<RateSnapshot>();
        Assert.Equal("EUR", snapshot!.BaseCurrency);
        Assert.NotEmpty(snapshot.Rates);
        Assert.DoesNotContain(snapshot.Rates, r => r.Currency == "EUR");
        Assert.All(snapshot.Rates, r => Assert.True(r.Rate > 0));
    }

    [Fact]
    public async Task Get_rates_with_unsupported_base_returns_bad_request()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/rates?base=XYZ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
