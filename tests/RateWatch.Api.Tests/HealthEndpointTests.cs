using System.Net;
using System.Net.Http.Json;

namespace RateWatch.Api.Tests;

public class HealthEndpointTests(RateWatchApiFactory factory)
    : IClassFixture<RateWatchApiFactory>
{
    [Fact]
    public async Task Get_health_returns_ok_status()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    private record HealthResponse(string Status);
}
