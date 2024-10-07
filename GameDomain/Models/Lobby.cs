namespace GameDomain.Models;

public class Lobby
{
    public long Id { get; }
    public string LobbyName { get; }
    public string? Password { get; }
    public List<Player> Players { get; } = [];
    public List<string> IpList { get; } = [];
    public List<Player> Spectators { get; } = [];

    public Lobby(long id, string ip, Player creator, string lobbyName, string? password)
    {
        Id = id;
        LobbyName = lobbyName;
        Password = password;
        IpList.Add(ip);
        Players.Add(creator);
    }
}