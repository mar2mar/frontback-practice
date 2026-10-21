namespace RateWatch.Api.Alerts;

/// <summary>Request body for <c>POST /api/alerts</c>. Validation happens in RateAlert and AlertService.</summary>
public record CreateAlertRequest(string BaseCurrency, string QuoteCurrency, decimal TargetRate);
