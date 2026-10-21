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
}
