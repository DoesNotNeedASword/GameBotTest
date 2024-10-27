using System.Text.Json.Serialization;

namespace GameDomain.Models;

public class Lobby
{
    public long Id { get; }
    public string LobbyName { get; }
    public string? Password { get; }
    public List<Player> Players { get; } = new();
    public List<string> IpList { get; } = new();
    public List<Player> Spectators { get; } = new();

    [JsonConstructor]
    public Lobby(long id, string lobbyName, string? password, List<Player> players, List<string> ipList, List<Player> spectators)
    {
        Id = id;
        LobbyName = lobbyName;
        Password = password;
        Players = players;
        IpList = ipList;
        Spectators = spectators;
    }

    public Lobby(long id, string ip, Player creator, string lobbyName, string? password)
    {
        Id = id;
        LobbyName = lobbyName;
        Password = password;
        IpList.Add(ip);
        Players.Add(creator);
    }
}
