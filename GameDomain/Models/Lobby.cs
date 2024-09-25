namespace GameDomain.Models;

public class Lobby
{
    public long Id { get; }
    public string LobbyName { get; }
    public string? Password { get; }
    public List<Player> Players { get; } = new();
    public List<Player> Spectators { get; } = new();

    public Lobby(long id, Player creator, string lobbyName, string? password)
    {
        Id = id;
        LobbyName = lobbyName;
        Password = password;
        Players.Add(creator);
    }
}