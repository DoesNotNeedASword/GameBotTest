using System.Text.Json.Serialization;

namespace Matchmaker.Models.Response;

public class EdgegapCreateResponse
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

    [JsonPropertyName("filters")]
    public List<object> Filters { get; set; } = new List<object>();
}
