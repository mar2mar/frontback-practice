namespace RateWatch.Api.Rates;

public record ExchangeRate(string Currency, decimal Rate);

public record RateSnapshot(string BaseCurrency, 
    DateTimeOffset Timestamp, 
    IReadOnlyList<ExchangeRate> Rates);
