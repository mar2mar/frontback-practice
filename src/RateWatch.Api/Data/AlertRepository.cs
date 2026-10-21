using RateWatch.Api.Alerts;

namespace RateWatch.Api.Data;

public class AlertRepository(AppDbContext db) : IAlertRepository
{
    public async Task AddAsync(RateAlert alert, CancellationToken cancellationToken = default)
    {
        db.RateAlerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<RateAlert>> ListAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task UpdateAsync(IEnumerable<RateAlert> alerts, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
