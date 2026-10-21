namespace RateWatch.Api.Alerts;

public interface IAlertService
{
    /// <exception cref="ArgumentException">Invalid or unsupported currency, or non-positive target rate.</exception>
    Task<RateAlert> CreateAsync(string baseCurrency, string quoteCurrency, decimal targetRate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RateAlert>> ListAsync(CancellationToken cancellationToken = default);

    /// <returns>False if no alert has the given id.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Checks all active alerts against current rates.</summary>
    /// <returns>The alerts triggered by this evaluation run.</returns>
    Task<IReadOnlyList<RateAlert>> EvaluateAsync(CancellationToken cancellationToken = default);
}
