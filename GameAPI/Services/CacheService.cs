using Microsoft.Extensions.Caching.Distributed;

namespace GameAPI.Services;

public class CacheService(IDistributedCache cache)
{
    public async Task<string?> GetAsync(string key)
    {
        return await cache.GetStringAsync(key);
    }

    public async void SetAsync(string key, string value)
    {
        var options = new DistributedCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5));
        await cache.SetStringAsync(key, value, options);
    }

    public async void RemoveAsync(string key)
    {
        await cache.RemoveAsync(key);
    }
}