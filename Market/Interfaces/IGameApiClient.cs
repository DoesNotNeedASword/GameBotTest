using GameDomain.Models;

namespace Market.Interfaces;

public interface IGameApiClient
{
    Task<Player?> GetPlayerAsync(long playerId);
    Task<bool> UpdatePlayerAsync(Player player);
    Task<bool> TransferCarAsync(string carId, long newOwnerId);
    Task<bool> TransferCurrencyAsync(long buyerId, long sellerId, int amount);
}