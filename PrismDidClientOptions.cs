namespace DidPrismResolverClient;

public class PrismDidClientOptions
{
    /// <summary>
    /// The base URL for the Prism Node (e.g., https://localhost:5001 or https://my.production.resolver/).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional: a default ledger to pass if none is explicitly provided.
    /// E.g. "mainnet", "preprod", ...
    /// </summary>
    public string? DefaultLedger { get; set; }
}