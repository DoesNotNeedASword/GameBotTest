using GameDomain.Models;

namespace GameDomain.Interfaces;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    public Task<List<Player>> GetTopPlayersAsync(int count = 100);
    Task<Player?> GetPlayerAsync(long id);
}
