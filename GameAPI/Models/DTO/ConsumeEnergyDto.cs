using System.Text.Json.Serialization;

namespace GameAPI.Models.DTO;

public class ConsumeEnergyDto
{
    [JsonPropertyName("playerId")]
    public long PlayerId { get; set; }
    [JsonPropertyName("value")]
    public int Value { get; set; }
}