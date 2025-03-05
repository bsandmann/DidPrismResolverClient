namespace DidPrismResolverClient;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Models;

public class PrismDidClient
{
    private readonly HttpClient _httpClient;
    private readonly PrismDidClientOptions _options;

    // We'll define a custom JSON serializer for safety, if you want.
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public PrismDidClient(HttpClient httpClient, PrismDidClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // If the user gave a base url in the options, set it:
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    /// <summary>
    /// Resolve a DID and return a DID Resolution Result (DID document + metadata).
    /// This approach sets the Accept header such that the server returns 
    /// the full DID Resolution Result.
    /// 
    /// If you prefer only the DidDocument, see the other method below.
    /// </summary>
    /// <param name="did">The DID to resolve</param>
    /// <param name="options">Optional resolution options, e.g. versionTime, versionId, etc.</param>
    /// <param name="ledger">Optional ledger to use (e.g. mainnet, preprod). 
    /// If null or empty, it falls back to _options.DefaultLedger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DidResolutionResult> ResolveDidFullAsync(
        string did,
        ResolutionOptions? options = null,
        string? ledger = null,
        CancellationToken cancellationToken = default)
    {
        ledger ??= _options.DefaultLedger;

        // We want a DID Resolution Result -> set Accept accordingly
        // The server is looking for "application/ld+json;profile=\"https://w3id.org/did-resolution\"" 
        // in order to return DidResolutionResult
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(did, options, ledger));
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("application/ld+json;profile=\"https://w3id.org/did-resolution\"");

        // Execute
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // Read out the status code to handle error states:
        if (response.IsSuccessStatusCode)
        {
            // Parse as DidResolutionResult
            var result = await response.Content.ReadFromJsonAsync<DidResolutionResult>(
                DefaultJsonSerializerOptions,
                cancellationToken
            ) ?? new DidResolutionResult()
            {
                DidDocument = null!,
            };

            return result;
        }
        else
        {
            // The server might return some JSON with {error = "..."} or a DidResolutionResult with an error field
            // or just a 404. You have a few options:
            // 1. Throw an exception
            // 2. Return a special error object
            // For demonstration, we'll throw an exception including the body text.

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PrismDidResolutionException(
                $"Error resolving DID. Status: {response.StatusCode}, Body: {body}");
        }
    }

    /// <summary>
    /// Resolve a DID but request only the DID document itself as JSON (or JSON-LD).
    /// This sets Accept to "application/did+ld+json" or "application/did+json".
    /// If the DID is deactivated, the server might respond with 410 Gone, etc.
    /// 
    /// If you want the entire resolution result with metadata, use ResolveDidFullAsync.
    /// </summary>
    /// <param name="did">The DID to resolve</param>
    /// <param name="options">Optional resolution options</param>
    /// <param name="ledger">Ledger to use, e.g. mainnet, preprod</param>
    /// <param name="acceptType">Either "application/did+ld+json" or "application/did+json"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A DidDocument object if resolution succeeds.</returns>
    public async Task<DidDocument> ResolveDidDocumentAsync(
        string did,
        ResolutionOptions? options = null,
        string? ledger = null,
        string acceptType = "application/did+ld+json",
        CancellationToken cancellationToken = default)
    {
        ledger ??= _options.DefaultLedger;

        // We want only the DID Document -> set Accept to e.g. application/did+ld+json
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(did, options, ledger));
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd(acceptType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            // parse DidDocument
            var document = await response.Content.ReadFromJsonAsync<DidDocument>(
                DefaultJsonSerializerOptions,
                cancellationToken
            ) ?? new DidDocument();

            return document;
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PrismDidResolutionException(
                $"Error resolving DID document. Status: {response.StatusCode}, Body: {body}");
        }
    }

    /// <summary>
    /// Build the URL (relative path + query string) for the call to /api/v{version}/identifiers/{did}.
    /// The default "version" is 1.0. 
    /// We'll pass query params for options.VersionId, options.VersionTime, etc. if present.
    /// We'll pass the ledger too if the user sets it or the config sets it.
    /// 
    /// Example final URL might be:
    ///   /api/v1.0/identifiers/did:prism:xxxx?versionId=abc&ledger=preprod
    /// </summary>
    private static string BuildUrl(string did, ResolutionOptions? options, string? ledger)
    {
        // Start with route
        var url = $"/api/v1.0/identifiers/{Uri.EscapeDataString(did)}";

        var queries = new List<string>();

        if (options is not null)
        {
            if (!string.IsNullOrEmpty(options.VersionId))
            {
                queries.Add($"versionId={Uri.EscapeDataString(options.VersionId)}");
            }

            if (options.VersionTime.HasValue)
            {
                // Format as e.g. 2025-01-12T18:22:37Z (or your desired format).
                var timeString = options.VersionTime.Value.ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ssZ");
                queries.Add($"versionTime={Uri.EscapeDataString(timeString)}");
            }

            if (options.IncludeNetworkIdentifier.HasValue)
            {
                // e.g. includeNetworkIdentifier=true/false
                queries.Add($"includeNetworkIdentifier={options.IncludeNetworkIdentifier.Value.ToString().ToLower()}");
            }
        }

        if (!string.IsNullOrWhiteSpace(ledger))
        {
            queries.Add($"ledger={Uri.EscapeDataString(ledger)}");
        }

        if (queries.Count > 0)
        {
            url += "?" + string.Join("&", queries);
        }

        return url;
    }
}
