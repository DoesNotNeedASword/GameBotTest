using System.Text.Json.Serialization;

namespace Matchmaker.Models.Response;

public class ContainerLogStorage
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("endpoint_storage")]
    public string EndpointStorage { get; set; }
}