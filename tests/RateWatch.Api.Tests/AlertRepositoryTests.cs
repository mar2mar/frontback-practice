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

    [Fact]
    public async Task List_returns_empty_list_when_there_are_no_alerts()
    {
        await using var db = new AppDbContext(_options);

        var alerts = await new AlertRepository(db).ListAsync();

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task List_returns_alerts_newest_first()
    {
        var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        await using (var db = new AppDbContext(_options))
        {
            var repository = new AlertRepository(db);
            await repository.AddAsync(new RateAlert("USD", "EUR", 0.95m, createdAt));
            await repository.AddAsync(new RateAlert("USD", "GBP", 0.80m, createdAt));
        }

        await using var readDb = new AppDbContext(_options);
        var alerts = await new AlertRepository(readDb).ListAsync();

        Assert.Equal(["GBP", "EUR"], alerts.Select(a => a.QuoteCurrency));
        Assert.True(alerts[0].Id > alerts[1].Id);
    }

    [Fact]
    public async Task Delete_removes_only_the_given_alert_and_returns_true()
    {
        var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var toDelete = new RateAlert("USD", "EUR", 0.95m, createdAt);
        var toKeep = new RateAlert("USD", "GBP", 0.80m, createdAt);
        await using (var db = new AppDbContext(_options))
        {
            var repository = new AlertRepository(db);
            await repository.AddAsync(toDelete);
            await repository.AddAsync(toKeep);
        }

        bool deleted;
        await using (var db = new AppDbContext(_options))
        {
            deleted = await new AlertRepository(db).DeleteAsync(toDelete.Id);
        }

        Assert.True(deleted);
        await using var readDb = new AppDbContext(_options);
        Assert.Equal(toKeep.Id, Assert.Single(await readDb.RateAlerts.ToListAsync()).Id);
    }

    [Fact]
    public async Task Delete_of_unknown_id_returns_false_and_keeps_existing_alerts()
    {
        var alert = new RateAlert("USD", "EUR", 0.95m, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
        await using (var db = new AppDbContext(_options))
        {
            await new AlertRepository(db).AddAsync(alert);
        }

        bool deleted;
        await using (var db = new AppDbContext(_options))
        {
            deleted = await new AlertRepository(db).DeleteAsync(alert.Id + 1);
        }

        Assert.False(deleted);
        await using var readDb = new AppDbContext(_options);
        Assert.Single(await readDb.RateAlerts.ToListAsync());
    }
}
