namespace Matchmaker.Interfaces;

public interface IApiClient
{
    Task<string?> GetPlayerRegionIpAsync(long playerId);
    Task<bool> UpdatePlayerRatingAsync(long telegramId, int ratingChange);
}
