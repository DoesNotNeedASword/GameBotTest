using System.Text.Json.Serialization;

namespace GameAPI.Verification.Model;

public class TgPayloadDto
{
    [JsonPropertyName("init_data")]
    public string? InitData { get; init; }
}