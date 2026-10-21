using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RateWatch.Api.Alerts;
using RateWatch.Api.Data;

namespace RateWatch.Api.Tests;

/// <summary>
/// Runs against an isolated in-memory SQLite database built from the real migrations.
/// </summary>
public sealed class AlertRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertRepositoryTests()
    {
        _connection.Open(); // the in-memory database lives as long as this connection
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = new AppDbContext(_options);
        db.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Add_assigns_id_and_alert_can_be_read_back()
    {
        var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var alert = new RateAlert("USD", "EUR", 0.123456m, createdAt);

        await using (var db = new AppDbContext(_options))
        {
            await new AlertRepository(db).AddAsync(alert);
        }

        Assert.NotEqual(0, alert.Id);
        await using var readDb = new AppDbContext(_options);
        var stored = await readDb.RateAlerts.SingleAsync();
        Assert.Equal(alert.Id, stored.Id);
        Assert.Equal("USD", stored.BaseCurrency);
        Assert.Equal("EUR", stored.QuoteCurrency);
        Assert.Equal(0.123456m, stored.TargetRate);
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.Null(stored.TriggeredAt);
    }
}
