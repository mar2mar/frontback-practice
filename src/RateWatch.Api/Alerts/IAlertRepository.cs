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

    /// <summary>Alerts that have not been triggered (TriggeredAt is null), oldest first (ordered by Id).</summary>
    Task<IReadOnlyList<RateAlert>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <returns>False if no alert has the given id.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets TriggeredAt (stored as UTC) only if the alert exists and has not been triggered yet.
    /// Safe under concurrent evaluation: at most one caller can trigger a given alert.
    /// </summary>
    /// <returns>True only if this call triggered the alert.</returns>
    Task<bool> TryMarkTriggeredAsync(int alertId, DateTimeOffset triggeredAt, CancellationToken cancellationToken = default);
}
