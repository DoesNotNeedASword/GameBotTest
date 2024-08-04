namespace GameDomain.Interfaces;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task UpdateLeaderboardAsync(string playerId, int rating);
    public Task<List<KeyValuePair<string, double>>> GetTopPlayersAsync(int count = 100);
}
