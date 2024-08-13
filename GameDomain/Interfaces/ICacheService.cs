using GameDomain.Models;

namespace GameDomain.Interfaces;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task UpdateLeaderboardAsync(long playerId, int rating);
    public Task<List<KeyValuePair<long, double>>> GetTopPlayersAsync(int count = 100);
    Task<Player?> GetPlayerAsync(long id);
}
