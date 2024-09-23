using System.Text.Json.Serialization;

namespace Matchmaker.Models.Response;

public class EdgegapDeploymentResponse
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; }

    [JsonPropertyName("request_dns")]
    public string RequestDns { get; set; }

    [JsonPropertyName("request_app")]
    public string RequestApp { get; set; }

    [JsonPropertyName("request_version")]
    public string RequestVersion { get; set; }

    [JsonPropertyName("request_user_count")]
    public int RequestUserCount { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }

    [JsonPropertyName("continent")]
    public string Continent { get; set; }

    [JsonPropertyName("administrative_division")]
    public string AdministrativeDivision { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }

    [JsonPropertyName("container_log_storage")]
    public ContainerLogStorage ContainerLogStorage { get; set; }
}