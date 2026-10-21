using RateWatch.Api.Rates;

namespace RateWatch.Api.Alerts;

public class AlertService(IAlertRepository repository, IExchangeRateProvider rateProvider, TimeProvider timeProvider)
    : IAlertService
{
    public async Task<RateAlert> CreateAsync(string baseCurrency, string quoteCurrency, decimal targetRate,
        CancellationToken cancellationToken = default)
    {
        // The entity validates and normalizes first, so the provider is only asked about well-formed codes.
        var alert = new RateAlert(baseCurrency, quoteCurrency, targetRate, timeProvider.GetUtcNow());

        var snapshot = await rateProvider.GetLatestRatesAsync(alert.BaseCurrency, cancellationToken)
            ?? throw new ArgumentException($"Unsupported base currency '{alert.BaseCurrency}'.", nameof(baseCurrency));

        if (!snapshot.Rates.Any(r => r.Currency == alert.QuoteCurrency))
        {
            throw new ArgumentException($"Unsupported quote currency '{alert.QuoteCurrency}'.", nameof(quoteCurrency));
        }

        await repository.AddAsync(alert, cancellationToken);
        return alert;
    }

    public Task<IReadOnlyList<RateAlert>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(cancellationToken);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);

    public async Task<IReadOnlyList<RateAlert>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var activeAlerts = await repository.ListActiveAsync(cancellationToken);

        // Phase 1: fetch every snapshot first (one call per distinct base), so a provider failure
        // aborts the run before any alert is persisted.
        var snapshots = new Dictionary<string, RateSnapshot?>();
        foreach (var baseCurrency in activeAlerts.Select(a => a.BaseCurrency).Distinct())
        {
            snapshots[baseCurrency] = await rateProvider.GetLatestRatesAsync(baseCurrency, cancellationToken);
        }

        // Phase 2: evaluate and conditionally persist, in active-alert order.
        var now = timeProvider.GetUtcNow();
        var triggered = new List<RateAlert>();
        foreach (var alert in activeAlerts)
        {
            var rate = snapshots[alert.BaseCurrency]?.Rates.FirstOrDefault(r => r.Currency == alert.QuoteCurrency);
            if (rate is null)
            {
                continue; // no rate available: skip, don't fail the run
            }

            // Trigger applies the rule in memory; the conditional update decides if this run wins.
            if (alert.Trigger(rate.Rate, now) && await repository.TryMarkTriggeredAsync(alert.Id, now, cancellationToken))
            {
                triggered.Add(alert);
            }
        }

        return triggered;
    }
}
