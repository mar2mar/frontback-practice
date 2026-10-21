using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateWatch.Api.Alerts;
using RateWatch.Api.Data;

namespace RateWatch.Api.Tests;

public class DatabaseStartupTests
{
    [Fact]
    public async Task Startup_creates_and_migrates_a_fresh_database()
    {
        using var factory = new RateWatchApiFactory();
        Assert.False(File.Exists(factory.DatabasePath));

        using var scope = factory.Services.CreateScope(); // accessing Services starts the app

        Assert.True(File.Exists(factory.DatabasePath));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await db.RateAlerts.ToListAsync());
    }

    [Fact]
    public async Task Restart_preserves_existing_alerts()
    {
        var databasePath = RateWatchApiFactory.NewTempDatabasePath();
        try
        {
            using (var firstRun = RateWatchApiFactory.WithDatabaseFile(databasePath))
            using (var scope = firstRun.Services.CreateScope())
            {
                var repository = new AlertRepository(scope.ServiceProvider.GetRequiredService<AppDbContext>());
                await repository.AddAsync(new RateAlert("USD", "EUR", 0.95m, DateTimeOffset.UtcNow));
            }

            using var secondRun = RateWatchApiFactory.WithDatabaseFile(databasePath);
            using var secondScope = secondRun.Services.CreateScope();
            var alerts = await secondScope.ServiceProvider.GetRequiredService<AppDbContext>().RateAlerts.ToListAsync();

            Assert.Equal("EUR", Assert.Single(alerts).QuoteCurrency);
        }
        finally
        {
            RateWatchApiFactory.DeleteDatabase(databasePath);
        }
    }
}
