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
        throw new NotImplementedException();

    public Task<IReadOnlyList<RateAlert>> EvaluateAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
