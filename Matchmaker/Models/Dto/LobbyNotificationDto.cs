namespace Matchmaker.Models.Dto;

public class LobbyNotificationDto(int statusCode, string message)
{
    public int StatusCode { get; set; } = statusCode;
    public string Message { get; set; } = message;
}

public enum LobbyNotificationStatus
{
    Heartbeat = 100,
    GameStarted = 200,
    PlayerConnected = 201,
    PlayerDisconnected = 202,
    LobbyClosed = 203,
    InvalidRequest = 400,
    LobbyNotFound = 404,
    WebSocketError = 500
}