using RateWatch.Api.Alerts;
using RateWatch.Api.Rates;
using RateWatch.Api.Tests.TestDoubles;

namespace RateWatch.Api.Tests;

public class AlertServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeAlertRepository _repository = new();
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        var rateProvider = new StubExchangeRateProvider();
        rateProvider.SetRates("USD", Now, new ExchangeRate("EUR", 0.92m), new ExchangeRate("GBP", 0.79m));
        _service = new AlertService(_repository, rateProvider, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Create_persists_and_returns_alert_for_supported_currencies()
    {
        var alert = await _service.CreateAsync("usd", "eur", 0.95m);

        Assert.Equal("USD", alert.BaseCurrency);
        Assert.Equal("EUR", alert.QuoteCurrency);
        Assert.Equal(0.95m, alert.TargetRate);
        Assert.Equal(Now, alert.CreatedAt);
        Assert.Null(alert.TriggeredAt);
        Assert.Same(alert, Assert.Single(_repository.Alerts));
    }

    [Fact]
    public async Task Create_with_unsupported_base_currency_throws_and_persists_nothing()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("CHF", "EUR", 0.95m));

        Assert.Equal("baseCurrency", ex.ParamName);
        Assert.Empty(_repository.Alerts);
    }

    [Fact]
    public async Task Create_with_unsupported_quote_currency_throws_and_persists_nothing()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("USD", "JPY", 0.95m));

        Assert.Equal("quoteCurrency", ex.ParamName);
        Assert.Empty(_repository.Alerts);
    }

    [Fact]
    public async Task Create_with_invalid_alert_values_throws_and_persists_nothing()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CreateAsync("USD", "EUR", 0m));

        Assert.Empty(_repository.Alerts);
    }
}
