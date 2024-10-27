using System.Text.Json;
using GameDomain.Models;
using Matchmaker.Interfaces;
using StackExchange.Redis;

public class LobbyCacheService : ILobbyCacheService
{
    private readonly IDatabase _redisDb;
    private const string LobbySortedSetKey = "lobbiesByPlayerCount";
    private const string LobbyIdCounterKey = "lastLobbyId";
    private const string LobbyKeyPrefix = "lobby:";
    private const string CreatorLobbyKeyPrefix = "creatorLobby:";

    public LobbyCacheService(IConnectionMultiplexer redisConnection)
    {
        _redisDb = redisConnection.GetDatabase();
    }

    public async Task<Lobby?> GetLobbyAsync(long lobbyId)
    {
        var lobbyJson = await _redisDb.StringGetAsync($"lobby:{lobbyId}");
        return string.IsNullOrEmpty(lobbyJson) ? null : JsonSerializer.Deserialize<Lobby>(lobbyJson);
    }

    public async Task SaveLobbyAsync(Lobby lobby)
    {
        var lobbyJson = JsonSerializer.Serialize(lobby);
        await _redisDb.StringSetAsync($"{LobbyKeyPrefix}{lobby.Id}", lobbyJson);

        await SaveCreatorLobbyAsync(lobby.Players.First().TelegramId, lobby.Id);
    }
    
    public async Task<List<Lobby>> GetAllLobbiesAsync(string? filter = null)
    {
        var server = _redisDb.Multiplexer.GetServer(_redisDb.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: "lobby:*");
    
        var lobbies = new List<Lobby>();

        foreach (var key in keys)
        {
            var lobbyJson = await _redisDb.StringGetAsync(key);
            if (string.IsNullOrEmpty(lobbyJson)) continue;
            var lobby = JsonSerializer.Deserialize<Lobby>(lobbyJson);
            if (lobby != null && (filter == null || lobby.LobbyName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                lobbies.Add(lobby);
            }
        }

        return lobbies;
    }
    public async Task DeleteLobbyAsync(long lobbyId)
    {
        var lobby = await GetLobbyAsync(lobbyId);
        if (lobby != null)
        {
            await _redisDb.KeyDeleteAsync($"{CreatorLobbyKeyPrefix}{lobby.Players.First().TelegramId}");
        }

        await _redisDb.KeyDeleteAsync($"{LobbyKeyPrefix}{lobbyId}");
    }

    private async Task SaveCreatorLobbyAsync(long creatorId, long lobbyId)
    {
        await _redisDb.StringSetAsync($"{CreatorLobbyKeyPrefix}{creatorId}", lobbyId);
    }

    public async Task<Lobby?> GetLobbyByCreatorIdAsync(long creatorId)
    {
        var lobbyId = await _redisDb.StringGetAsync($"{CreatorLobbyKeyPrefix}{creatorId}");
        if (lobbyId.IsNullOrEmpty) return null;

        return await GetLobbyAsync((long)lobbyId);
    }

    public async Task UpdateLobbyPlayerCountAsync(long lobbyId, int playerCount)
    {
        await _redisDb.SortedSetAddAsync(LobbySortedSetKey, lobbyId.ToString(), playerCount);
    }

    public async Task DeleteLobbyPlayerCountAsync(long lobbyId)
    {
        await _redisDb.SortedSetRemoveAsync(LobbySortedSetKey, lobbyId.ToString());
    }

    public async Task<Lobby?> FindLobbyWithSinglePlayerAsync()
    {
        var lobbyIds = await _redisDb.SortedSetRangeByScoreAsync(LobbySortedSetKey, 1, 1, Exclude.None, Order.Ascending, 0, 1);

        if (lobbyIds.Length == 0)
        {
            return null;
        }

        var lobbyId = long.Parse(lobbyIds[0]);
        return await GetLobbyAsync(lobbyId);
    }

    public async Task<long> GenerateNewLobbyIdAsync()
    {
        return await _redisDb.StringIncrementAsync(LobbyIdCounterKey);
    }

    public async Task<long?> GetPlayerLobbyAsync(string playerLobbyKey)
    {
        var lobbyId = await _redisDb.StringGetAsync(playerLobbyKey);
        return long.TryParse(lobbyId, out var id) ? id : null;
    }
    
    public async Task SetPlayerLobbyAsync(string playerLobbyKey, long lobbyId)
    {
        await _redisDb.StringSetAsync(playerLobbyKey, lobbyId.ToString());
    }

    public async Task RemovePlayerLobbyAsync(string playerLobbyKey)
    {
        await _redisDb.KeyDeleteAsync(playerLobbyKey);
    }
}
