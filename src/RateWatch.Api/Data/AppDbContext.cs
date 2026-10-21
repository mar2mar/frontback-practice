using Microsoft.EntityFrameworkCore;
using RateWatch.Api.Alerts;

namespace RateWatch.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RateAlert> RateAlerts => Set<RateAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var alert = modelBuilder.Entity<RateAlert>();
        alert.ToTable("RateAlerts");
        alert.Property(a => a.BaseCurrency).HasMaxLength(3);
        alert.Property(a => a.QuoteCurrency).HasMaxLength(3);
        alert.Property(a => a.TargetRate).HasPrecision(18, 6);
    }
}
