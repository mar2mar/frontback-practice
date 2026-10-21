using Microsoft.EntityFrameworkCore;
using RateWatch.Api.Alerts;

namespace RateWatch.Api.Data;

public class AlertRepository(AppDbContext db) : IAlertRepository
{
    public async Task AddAsync(RateAlert alert, CancellationToken cancellationToken = default)
    {
        db.RateAlerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RateAlert>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.RateAlerts
            .AsNoTracking()
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        await db.RateAlerts
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public Task UpdateAsync(IEnumerable<RateAlert> alerts, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
