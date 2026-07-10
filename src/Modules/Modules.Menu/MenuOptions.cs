namespace Modules.Menu;

/// <summary>
/// Bound from the "Menu" configuration section by the host. A single
/// tax rate for now — per-branch tax configuration is explicitly deferred.
/// CurrencyCode is an opaque ISO 4217 code; formatting is entirely a
/// frontend concern.
/// </summary>
public sealed record MenuOptions(decimal TaxRatePercent, string CurrencyCode);
