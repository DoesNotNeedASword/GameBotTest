﻿using GameDomain.Models.DTOs;

namespace Market.Interfaces;

public interface IMarketService
{
    Task<bool> ListCarAsync(ListingDto listing);
    Task<List<ListingDto>> GetAllListingsAsync();
    Task<ListingDto?> GetListingAsync(Guid listingId);
    Task<bool> PurchaseCarAsync(long buyerId, Guid listingId);

    Task<bool> RemoveListingAsync(string id);
}