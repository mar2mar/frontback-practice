using Microsoft.EntityFrameworkCore;
using RateWatch.Api.Alerts;
using RateWatch.Api.Data;
using RateWatch.Api.Rates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlite(builder.Configuration.GetConnectionString("RateWatch")));

builder.Services.AddSingleton<IExchangeRateProvider>(
    _ => new FakeExchangeRateProvider(TimeProvider.System, Random.Shared));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IAlertService, AlertService>();

var app = builder.Build();

// Create the SQLite database if needed and apply pending migrations; existing data is kept.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/rates", async (IExchangeRateProvider provider, CancellationToken cancellationToken, string @base = "USD") =>
{
    var snapshot = await provider.GetLatestRatesAsync(@base, cancellationToken);
    return snapshot is null
        ? Results.Problem($"Unsupported base currency '{@base}'.", statusCode: StatusCodes.Status400BadRequest)
        : Results.Ok(snapshot);
});

app.MapPost("/api/alerts", async (CreateAlertRequest request, IAlertService alerts, CancellationToken cancellationToken) =>
{
    try
    {
        var alert = await alerts.CreateAsync(request.BaseCurrency, request.QuoteCurrency, request.TargetRate, cancellationToken);
        return Results.Created($"/api/alerts/{alert.Id}", alert);
    }
    catch (ArgumentException ex) // invalid or unsupported currency, non-positive target rate
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapGet("/api/alerts", async (IAlertService alerts, CancellationToken cancellationToken) =>
    Results.Ok(await alerts.ListAsync(cancellationToken)));

app.MapDelete("/api/alerts/{id:int}", async (int id, IAlertService alerts, CancellationToken cancellationToken) =>
    await alerts.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

app.Run();
