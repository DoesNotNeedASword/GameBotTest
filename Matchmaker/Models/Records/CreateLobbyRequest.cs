using GameDomain.Models;

public record CreateLobbyRequest(Player Creator, string LobbyName, string? Password, string Ip = "87.228.27.97");