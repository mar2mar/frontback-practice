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
        throw new NotImplementedException();

    public Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task UpdateAsync(IEnumerable<RateAlert> alerts, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
