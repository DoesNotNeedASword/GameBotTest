using GameDomain.Models.DTOs;

namespace Market.Interfaces;

public interface IMarketService
{
    Task<bool> ListCarAsync(ListingDto listing);
    Task<List<ListingDto>> GetAllListingsAsync();
    Task<bool> RemoveListingAsync(string id);
}