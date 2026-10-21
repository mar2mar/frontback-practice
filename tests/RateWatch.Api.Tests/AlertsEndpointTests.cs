using System.Net;
using System.Net.Http.Json;

namespace RateWatch.Api.Tests;

/// <summary>
/// HTTP tests for the alert CRUD endpoints. Each test uses its own factory, so its own empty SQLite database.
/// Validation details are covered by the RateAlert and AlertService unit tests.
/// </summary>
public class AlertsEndpointTests
{
    [Fact]
    public async Task Post_creates_alert_and_returns_201_with_location()
    {
        using var factory = new RateWatchApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/alerts",
            new { baseCurrency = "usd", quoteCurrency = "EUR", targetRate = 0.95m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var alert = await response.Content.ReadFromJsonAsync<AlertResponse>();
        Assert.NotEqual(0, alert!.Id);
        Assert.Equal(("USD", "EUR", 0.95m), (alert.BaseCurrency, alert.QuoteCurrency, alert.TargetRate));
        Assert.Null(alert.TriggeredAt);
        Assert.Equal($"/api/alerts/{alert.Id}", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("USD", "XYZ", 0.95)] // unsupported currency (service check)
    [InlineData("USD", "EUR", 0)]    // non-positive target rate (entity check)
    public async Task Post_with_invalid_alert_returns_400_problem(string baseCurrency, string quoteCurrency, decimal targetRate)
    {
        using var factory = new RateWatchApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/alerts", new { baseCurrency, quoteCurrency, targetRate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_returns_empty_array_then_persisted_alerts_newest_first()
    {
        using var factory = new RateWatchApiFactory();
        var client = factory.CreateClient();

        var empty = await client.GetAsync("/api/alerts");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("[]", await empty.Content.ReadAsStringAsync());

        await CreateAlertAsync(client, "EUR");
        await CreateAlertAsync(client, "GBP");

        var alerts = await client.GetFromJsonAsync<List<AlertResponse>>("/api/alerts");
        Assert.Equal(["GBP", "EUR"], alerts!.Select(a => a.QuoteCurrency));
    }

    [Fact]
    public async Task Delete_existing_alert_returns_204_and_removes_it()
    {
        using var factory = new RateWatchApiFactory();
        var client = factory.CreateClient();
        var alert = await CreateAlertAsync(client, "EUR");

        var response = await client.DeleteAsync($"/api/alerts/{alert.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<AlertResponse>>("/api/alerts"))!);
    }

    [Fact]
    public async Task Delete_unknown_alert_returns_404_and_keeps_existing_alerts()
    {
        using var factory = new RateWatchApiFactory();
        var client = factory.CreateClient();
        var alert = await CreateAlertAsync(client, "EUR");

        var response = await client.DeleteAsync($"/api/alerts/{alert.Id + 1}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<AlertResponse>>("/api/alerts"))!);
    }

    private static async Task<AlertResponse> CreateAlertAsync(HttpClient client, string quoteCurrency)
    {
        var response = await client.PostAsJsonAsync("/api/alerts",
            new { baseCurrency = "USD", quoteCurrency, targetRate = 1.5m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AlertResponse>())!;
    }
}
