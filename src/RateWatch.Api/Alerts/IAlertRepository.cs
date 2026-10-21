namespace RateWatch.Api.Alerts;

/// <summary>
/// Persistence for rate alerts. Each method saves its own changes.
/// </summary>
public interface IAlertRepository
{
    /// <summary>Saves a new alert and assigns its <see cref="RateAlert.Id"/>.</summary>
    Task AddAsync(RateAlert alert, CancellationToken cancellationToken = default);

    /// <summary>All alerts, newest first (ordered by Id).</summary>
    Task<IReadOnlyList<RateAlert>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Alerts that have not been triggered (TriggeredAt is null).</summary>
    Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <returns>False if no alert has the given id.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to existing alerts, e.g. alerts marked as triggered.</summary>
    Task UpdateAsync(IEnumerable<RateAlert> alerts, CancellationToken cancellationToken = default);
}
