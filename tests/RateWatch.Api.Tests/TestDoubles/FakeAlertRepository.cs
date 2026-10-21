using RateWatch.Api.Alerts;

namespace RateWatch.Api.Tests.TestDoubles;

/// <summary>
/// In-memory repository for service tests. Alerts have no ids (Id is assigned by the database),
/// so id-based methods record their calls and return values set by the test.
/// </summary>
public class FakeAlertRepository : IAlertRepository
{
    public List<RateAlert> Alerts { get; } = [];

    public Task AddAsync(RateAlert alert, CancellationToken cancellationToken = default)
    {
        Alerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RateAlert>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RateAlert>>(Alerts.ToList());

    /// <summary>
    /// Returns detached copies, like the real repository's AsNoTracking query, so changes made by the
    /// caller (e.g. RateAlert.Trigger) don't leak into the stored alerts. The constructor copy is exact here:
    /// fake alerts all have Id 0, and active alerts have TriggeredAt null.
    /// </summary>
    public Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RateAlert>>(Alerts
            .Where(a => a.TriggeredAt is null)
            .Select(a => new RateAlert(a.BaseCurrency, a.QuoteCurrency, a.TargetRate, a.CreatedAt))
            .ToList());

    /// <summary>Ids passed to <see cref="DeleteAsync"/>. Alerts here have no ids, so nothing is matched or removed.</summary>
    public List<int> DeletedIds { get; } = [];

    /// <summary>The value <see cref="DeleteAsync"/> returns.</summary>
    public bool DeleteResult { get; set; }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
        return Task.FromResult(DeleteResult);
    }

    /// <summary>Calls to <see cref="TryMarkTriggeredAsync"/>. Alerts here have no ids, so nothing is matched.</summary>
    public List<(int AlertId, DateTimeOffset TriggeredAt)> MarkTriggeredCalls { get; } = [];

    /// <summary>The value <see cref="TryMarkTriggeredAsync"/> returns; false simulates losing a concurrent race.</summary>
    public bool MarkTriggeredResult { get; set; } = true;

    public Task<bool> TryMarkTriggeredAsync(int alertId, DateTimeOffset triggeredAt,
        CancellationToken cancellationToken = default)
    {
        MarkTriggeredCalls.Add((alertId, triggeredAt));
        return Task.FromResult(MarkTriggeredResult);
    }
}
