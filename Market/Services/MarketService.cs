using GameDomain.Models.DTOs;
using Market.Interfaces;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Market.Services;

public class MarketService(IConnectionMultiplexer redis, IGameApiClient gameApiClient) : IMarketService
{
    private readonly IDatabase _database = redis.GetDatabase();
    private const string HashKey = "marketplace:items"; 
    private const string IndexKey = "marketplace:index"; 

    public async Task<bool> ListCarAsync(ListingDto listing)
    {
        var serializedListing = JsonConvert.SerializeObject(listing);

        await _database.HashSetAsync(HashKey, listing.Id.ToString(), serializedListing);

        await _database.SortedSetAddAsync(IndexKey, listing.Id.ToString(), listing.Price);

        return true;
    }

    public async Task<List<ListingDto>> GetAllListingsAsync()
    {
        var allItems = await _database.HashGetAllAsync(HashKey);
        
        return allItems
            .Select(entry => JsonConvert.DeserializeObject<ListingDto>(entry.Value))
            .Where(item => item != null)
            .ToList()!;
    }

    public async Task<ListingDto?> GetListingAsync(Guid listingId)
    {
        var serializedListing = await _database.HashGetAsync(HashKey, listingId.ToString());
        return serializedListing.IsNullOrEmpty 
            ? null 
            : JsonConvert.DeserializeObject<ListingDto>(serializedListing);
    }

    public async Task<bool> RemoveListingAsync(string id)
    {
        var hashResult = await _database.HashDeleteAsync(HashKey, id);

        var indexResult = await _database.SortedSetRemoveAsync(IndexKey, id);

        return hashResult && indexResult;
    }

    public async Task<bool> PurchaseCarAsync(long buyerId, Guid listingId)
    {
        var listing = await GetListingAsync(listingId);
        if (listing == null) return false;
        
        var transactionSuccess = await gameApiClient.TransferCurrencyAsync(buyerId, listing.SellerId, listing.Price);
        if (!transactionSuccess) return false;

        transactionSuccess = await gameApiClient.TransferCarAsync(listing.CarId, buyerId);
        if (!transactionSuccess)
        {
            await gameApiClient.TransferCurrencyAsync(listing.SellerId, buyerId, listing.Price);
            return false;
        }

        await RemoveListingAsync(listingId.ToString());

        return true;
    }

}
