using GameAPI.Options;
using GameDomain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;
using GameDomain.Models;

namespace GameAPI.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly PlayerService _playerService; 
    private const string LeaderboardKey = "players:leaderboard";
    private const string PlayerKeyPrefix = "player:";

    public CacheService(IDistributedCache cache, PlayerService playerService)
    {
        _cache = cache;
        _playerService = playerService;
    }
    public async Task<Player?> GetPlayerAsync(long id)
    {
        var cacheKey = $"{PlayerKeyPrefix}{id}";
        var cachedPlayer = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedPlayer))
        {
            return JsonSerializer.Deserialize<Player>(cachedPlayer);
        }

        var player = await _playerService.GetAsync(id);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(player), new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(1) });
        return player;
    }
    public async Task<List<KeyValuePair<long, double>>> GetTopPlayersAsync(int count = 100)
    {
        // Пытаемся получить данные из кэша
        var cachedData = await _cache.GetStringAsync(LeaderboardKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<KeyValuePair<long, double>>>(cachedData);
        }
        
        // Получение данных из базы данных, если кэш пуст
        var playersFromDb = await _playerService.GetTopPlayers(count);
        var players = playersFromDb.Select(p => new KeyValuePair<long, double>(p.TelegramId, p.Rating)).ToList();
        
        // Сериализация и кэширование данных
        var serializedData = JsonSerializer.Serialize(players);
        await SetAsync(LeaderboardKey, serializedData, TimeSpan.FromMinutes(5));
        
        return players;
    }
    
    public async Task<string?> GetAsync(string key)
    {
        return await _cache.GetStringAsync(key);
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiration = null)
    {
        var options = new DistributedCacheEntryOptions()
            .SetSlidingExpiration(expiration ?? TimeSpan.FromMinutes(5));
        await _cache.SetStringAsync(key, value, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task UpdateLeaderboardAsync(long playerId, int rating)
    {
        var leaders = await GetTopPlayersAsync();

        var playerIndex = leaders.FindIndex(p => p.Key == playerId);
        if (playerIndex >= 0)
        {
            var updatedPlayer = leaders[playerIndex];
            leaders[playerIndex] = new KeyValuePair<long, double>(playerId, updatedPlayer.Value + rating);

            await SetAsync(LeaderboardKey, JsonSerializer.Serialize(leaders), TimeSpan.FromMinutes(5));
        }
    }
}
