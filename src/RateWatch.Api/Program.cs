using RateWatch.Api.Rates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IExchangeRateProvider>(
    _ => new FakeExchangeRateProvider(TimeProvider.System, Random.Shared));

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/rates", async (IExchangeRateProvider provider, CancellationToken cancellationToken, string @base = "USD") =>
{
    var snapshot = await provider.GetLatestRatesAsync(@base, cancellationToken);
    return snapshot is null
        ? Results.Problem($"Unsupported base currency '{@base}'.", statusCode: StatusCodes.Status400BadRequest)
        : Results.Ok(snapshot);
});

app.Run();
