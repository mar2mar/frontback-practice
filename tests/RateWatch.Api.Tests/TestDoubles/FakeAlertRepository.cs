using RateWatch.Api.Alerts;

namespace RateWatch.Api.Tests.TestDoubles;

/// <summary>
/// In-memory repository for service tests. Only the methods used so far are implemented.
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

    public Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <summary>Ids passed to <see cref="DeleteAsync"/>. Alerts here have no ids, so nothing is matched or removed.</summary>
    public List<int> DeletedIds { get; } = [];

    /// <summary>The value <see cref="DeleteAsync"/> returns.</summary>
    public bool DeleteResult { get; set; }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
        return Task.FromResult(DeleteResult);
    }

    public Task UpdateAsync(IEnumerable<RateAlert> alerts, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
