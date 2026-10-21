using RateWatch.Api.Alerts;

namespace RateWatch.Api.Tests;

public class RateAlertTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_alert_keeps_its_values_and_is_not_triggered()
    {
        var alert = new RateAlert("USD", "EUR", 0.95m, CreatedAt);

        Assert.Equal("USD", alert.BaseCurrency);
        Assert.Equal("EUR", alert.QuoteCurrency);
        Assert.Equal(0.95m, alert.TargetRate);
        Assert.Equal(CreatedAt, alert.CreatedAt);
        Assert.Null(alert.TriggeredAt);
    }

    [Fact]
    public void Currencies_are_normalized_to_uppercase()
    {
        var alert = new RateAlert("usd", "eUr", 0.95m, CreatedAt);

        Assert.Equal("USD", alert.BaseCurrency);
        Assert.Equal("EUR", alert.QuoteCurrency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("US")]
    [InlineData("USDX")]
    [InlineData("U5D")]
    [InlineData(" US")]
    [InlineData("ÜSD")]
    public void Invalid_base_currency_throws(string? baseCurrency)
    {
        var ex = Assert.Throws<ArgumentException>(() => new RateAlert(baseCurrency!, "EUR", 0.95m, CreatedAt));

        Assert.Equal("baseCurrency", ex.ParamName);
    }

    [Fact]
    public void Invalid_quote_currency_throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RateAlert("USD", "EU", 0.95m, CreatedAt));

        Assert.Equal("quoteCurrency", ex.ParamName);
    }

    [Fact]
    public void Identical_currencies_after_normalization_throw()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RateAlert("USD", "usd", 0.95m, CreatedAt));

        Assert.Equal("quoteCurrency", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Non_positive_target_rate_throws(decimal targetRate)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RateAlert("USD", "EUR", targetRate, CreatedAt));

        Assert.Equal("targetRate", ex.ParamName);
    }

    [Fact]
    public void Smallest_positive_target_rate_is_accepted()
    {
        var alert = new RateAlert("USD", "EUR", 0.000001m, CreatedAt);

        Assert.Equal(0.000001m, alert.TargetRate);
    }

    [Fact]
    public void CreatedAt_is_normalized_to_utc()
    {
        var local = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(2));

        var alert = new RateAlert("USD", "EUR", 0.95m, local);

        Assert.Equal(TimeSpan.Zero, alert.CreatedAt.Offset);
        Assert.Equal(local.UtcDateTime, alert.CreatedAt.UtcDateTime);
    }

    [Fact]
    public void Trigger_above_target_returns_true_and_sets_TriggeredAt()
    {
        var alert = new RateAlert("USD", "EUR", 0.95m, CreatedAt);
        var now = CreatedAt.AddHours(1);

        Assert.True(alert.Trigger(0.950001m, now));
        Assert.Equal(now, alert.TriggeredAt);
    }

    [Theory]
    [InlineData(0.95)] // equal: strict comparison
    [InlineData(0.94)]
    public void Trigger_at_or_below_target_returns_false_and_stays_active(decimal currentRate)
    {
        var alert = new RateAlert("USD", "EUR", 0.95m, CreatedAt);

        Assert.False(alert.Trigger(currentRate, CreatedAt.AddHours(1)));
        Assert.Null(alert.TriggeredAt);
    }

    [Fact]
    public void Trigger_on_already_triggered_alert_returns_false_and_keeps_original_TriggeredAt()
    {
        var alert = new RateAlert("USD", "EUR", 0.95m, CreatedAt);
        var firstTrigger = CreatedAt.AddHours(1);
        alert.Trigger(0.96m, firstTrigger);

        Assert.False(alert.Trigger(1.10m, CreatedAt.AddHours(2)));
        Assert.Equal(firstTrigger, alert.TriggeredAt);
    }

    [Fact]
    public void Trigger_normalizes_TriggeredAt_to_utc()
    {
        var alert = new RateAlert("USD", "EUR", 0.95m, CreatedAt);
        var local = new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.FromHours(2));

        alert.Trigger(0.96m, local);

        Assert.Equal(TimeSpan.Zero, alert.TriggeredAt!.Value.Offset);
        Assert.Equal(local.UtcDateTime, alert.TriggeredAt.Value.UtcDateTime);
    }
}
