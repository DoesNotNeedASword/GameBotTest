using GameDomain.Models;

namespace GameBotTest.GameHttpClient;

public interface IGameApiClient
{
    Task<Player?> RegisterPlayerAsync(long telegramId, string name, long referrerId);
    Task<long> GetPlayerId(long telegramId);
}