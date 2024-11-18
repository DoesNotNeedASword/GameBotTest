using System.Text.Json;
using GameDomain.Models.DTOs;
using Market.Interfaces;
using StackExchange.Redis;

namespace Market.Services;

public class MarketService : IMarketService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public MarketService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = _redis.GetDatabase();
    }

    public async Task<bool> ListCarAsync(ListingDto listing)
    {
        var id = listing.Id.ToString();
        var json = JsonSerializer.Serialize(listing);
        return await _db.HashSetAsync("marketplace", id, json);
    }

    public async Task<List<ListingDto>> GetAllListingsAsync()
    {
        var entries = await _db.HashGetAllAsync("marketplace");

        return entries.Select(entry => JsonSerializer.Deserialize<ListingDto>(entry.Value)).OfType<ListingDto>().ToList();
    }

    public async Task<bool> RemoveListingAsync(string id)
    {
        return await _db.HashDeleteAsync("marketplace", id);
    }
}