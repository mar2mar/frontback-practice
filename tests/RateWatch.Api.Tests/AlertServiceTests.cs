using RateWatch.Api.Alerts;
using RateWatch.Api.Rates;
using RateWatch.Api.Tests.TestDoubles;

namespace RateWatch.Api.Tests;

public class AlertServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeAlertRepository _repository = new();
    private readonly StubExchangeRateProvider _rateProvider = new();
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        _rateProvider.SetRates("USD", Now, new ExchangeRate("EUR", 0.92m), new ExchangeRate("GBP", 0.79m));
        _service = new AlertService(_repository, _rateProvider, new FixedTimeProvider(Now));
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

    [Fact]
    public async Task List_returns_the_repository_alerts_in_repository_order()
    {
        var first = new RateAlert("USD", "GBP", 0.80m, Now);
        var second = new RateAlert("USD", "EUR", 0.95m, Now);
        _repository.Alerts.AddRange([first, second]);

        var alerts = await _service.ListAsync();

        Assert.Equal([first, second], alerts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_passes_the_id_to_the_repository_and_returns_its_result(bool repositoryResult)
    {
        _repository.DeleteResult = repositoryResult;

        var deleted = await _service.DeleteAsync(42);

        Assert.Equal(repositoryResult, deleted);
        Assert.Equal([42], _repository.DeletedIds);
    }

    // Evaluation. USD rates from the constructor: EUR 0.92, GBP 0.79.
    // FakeAlertRepository returns copies and never marks its stored alerts, so these tests cover one run only.

    [Fact]
    public async Task Evaluate_triggers_persists_and_returns_alerts_above_target_in_active_order()
    {
        AddAlert("USD", "GBP", 0.70m);
        AddAlert("USD", "EUR", 0.90m);

        var triggered = await _service.EvaluateAsync();

        Assert.Equal(["GBP", "EUR"], triggered.Select(a => a.QuoteCurrency));
        Assert.All(triggered, a => Assert.Equal(Now, a.TriggeredAt));
        Assert.Equal([(0, Now), (0, Now)], _repository.MarkTriggeredCalls); // fake alerts have Id 0
    }

    [Fact]
    public async Task Evaluate_does_not_trigger_alerts_at_or_below_target()
    {
        AddAlert("USD", "EUR", 0.92m); // equal: strict comparison
        AddAlert("USD", "GBP", 0.80m); // below

        Assert.Empty(await _service.EvaluateAsync());
        Assert.Empty(_repository.MarkTriggeredCalls);
    }

    [Fact]
    public async Task Evaluate_skips_alerts_without_a_rate()
    {
        AddAlert("USD", "JPY", 0.01m); // quote missing from the USD snapshot
        AddAlert("CHF", "EUR", 0.01m); // no snapshot for CHF

        Assert.Empty(await _service.EvaluateAsync());
        Assert.Empty(_repository.MarkTriggeredCalls);
    }

    [Fact]
    public async Task Evaluate_fetches_rates_once_per_distinct_base_currency()
    {
        _rateProvider.SetRates("EUR", Now, new ExchangeRate("USD", 1.09m));
        AddAlert("USD", "EUR", 0.90m);
        AddAlert("EUR", "USD", 1.00m);
        AddAlert("USD", "GBP", 0.70m);

        await _service.EvaluateAsync();

        Assert.Equal(["USD", "EUR"], _rateProvider.RequestedBases);
    }

    [Fact]
    public async Task Evaluate_provider_failure_throws_before_any_alert_is_persisted()
    {
        _rateProvider.ThrowFor("EUR");
        AddAlert("USD", "EUR", 0.90m); // would trigger: USD rates are available
        AddAlert("EUR", "USD", 1.00m); // its base currency fails

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EvaluateAsync());

        Assert.Empty(_repository.MarkTriggeredCalls);
    }

    [Fact]
    public async Task Evaluate_excludes_alert_when_conditional_update_loses()
    {
        _repository.MarkTriggeredResult = false; // another evaluation triggered it first
        AddAlert("USD", "EUR", 0.90m);

        var triggered = await _service.EvaluateAsync();

        Assert.Empty(triggered);
        Assert.Single(_repository.MarkTriggeredCalls);
    }

    [Fact]
    public async Task Evaluate_with_no_active_alerts_returns_empty_without_fetching_rates()
    {
        Assert.Empty(await _service.EvaluateAsync());
        Assert.Empty(_rateProvider.RequestedBases);
    }

    private void AddAlert(string baseCurrency, string quoteCurrency, decimal targetRate) =>
        _repository.Alerts.Add(new RateAlert(baseCurrency, quoteCurrency, targetRate, Now));
}
