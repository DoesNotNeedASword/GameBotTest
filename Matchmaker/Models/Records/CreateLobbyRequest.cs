using GameDomain.Models;

public record CreateLobbyRequest(Player Creator, string LobbyName, string? Password);