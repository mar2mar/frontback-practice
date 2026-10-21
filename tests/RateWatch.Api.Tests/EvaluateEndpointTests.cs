using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RateWatch.Api.Rates;
using RateWatch.Api.Tests.TestDoubles;

namespace RateWatch.Api.Tests;

/// <summary>
/// HTTP tests for POST /api/alerts/evaluate. xUnit creates a new instance per test, so each test gets
/// its own app and empty SQLite database. Rates come from a deterministic stub (USD: EUR 0.92, GBP 0.79, JPY 150).
/// </summary>
public sealed class EvaluateEndpointTests : IDisposable
{
    private readonly RateWatchApiFactory _factory = new();
    private readonly HttpClient _client;

    public EvaluateEndpointTests()
    {
        var rates = new StubExchangeRateProvider();
        rates.SetRates("USD", DateTimeOffset.UtcNow,
            new ExchangeRate("EUR", 0.92m), new ExchangeRate("GBP", 0.79m), new ExchangeRate("JPY", 150m));
        _client = _factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider>(rates)))
            .CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Evaluate_with_no_alerts_returns_200_and_empty_array()
    {
        var response = await _client.PostAsync("/api/alerts/evaluate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Evaluate_returns_alert_triggered_by_this_request()
    {
        var created = await CreateAlertAsync("EUR", 0.90m);

        var triggered = await EvaluateAsync();

        var alert = Assert.Single(triggered);
        Assert.NotEqual(0, alert.Id);
        Assert.Equal(created.Id, alert.Id);
        Assert.NotNull(alert.TriggeredAt);
    }

    [Fact]
    public async Task Evaluate_again_returns_empty_and_keeps_original_TriggeredAt()
    {
        await CreateAlertAsync("EUR", 0.90m);
        var firstTriggeredAt = Assert.Single(await EvaluateAsync()).TriggeredAt;

        Assert.Empty(await EvaluateAsync());

        var stored = Assert.Single((await _client.GetFromJsonAsync<List<AlertResponse>>("/api/alerts"))!);
        Assert.Equal(firstTriggeredAt, stored.TriggeredAt);
    }

    [Fact]
    public async Task Evaluate_does_not_trigger_alert_whose_target_equals_the_rate()
    {
        await CreateAlertAsync("EUR", 0.92m);

        Assert.Empty(await EvaluateAsync());
    }

    [Fact]
    public async Task Evaluate_returns_only_qualifying_alerts_in_ascending_id_order()
    {
        var eur = await CreateAlertAsync("EUR", 0.90m);  // 0.92 > 0.90: triggers
        await CreateAlertAsync("GBP", 0.80m);            // 0.79 < 0.80: stays active
        var jpy = await CreateAlertAsync("JPY", 100m);   // 150 > 100: triggers

        var triggered = await EvaluateAsync();

        Assert.Equal([eur.Id, jpy.Id], triggered.Select(a => a.Id));
        Assert.True(eur.Id < jpy.Id);
    }

    private async Task<List<AlertResponse>> EvaluateAsync()
    {
        var response = await _client.PostAsync("/api/alerts/evaluate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AlertResponse>>())!;
    }

    private async Task<AlertResponse> CreateAlertAsync(string quoteCurrency, decimal targetRate)
    {
        var response = await _client.PostAsJsonAsync("/api/alerts", new { baseCurrency = "USD", quoteCurrency, targetRate });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AlertResponse>())!;
    }
}
