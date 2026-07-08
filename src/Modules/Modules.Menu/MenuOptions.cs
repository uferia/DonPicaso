namespace Modules.Menu;

/// <summary>
/// Bound from the "Menu" configuration section by the host. A single
/// tax rate for now — per-branch tax configuration is explicitly deferred.
/// </summary>
public sealed record MenuOptions(decimal TaxRatePercent);
