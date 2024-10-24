using System.Text.Json.Serialization;

namespace Matchmaker.Models.Dto;

public class SpectatorNotificationDto(string message)
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = message;
}