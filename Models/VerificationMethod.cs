namespace DidPrismResolverClient.Models;

using System.Text.Json.Serialization;

public class VerificationMethod
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonPropertyName("controller")]
    public string Controller { get; set; }
    [JsonPropertyName("publicKeyJwk")]
    public PublicKeyJwk? PublicKeyJwk { get; set; }
}