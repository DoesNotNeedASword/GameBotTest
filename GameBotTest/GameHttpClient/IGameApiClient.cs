using GameDomain.Models;

namespace GameBotTest.GameHttpClient;

public interface IGameApiClient
{
    Task<Player?> RegisterPlayerAsync(long telegramId, string name, string referrerId = null);
    Task<string> GetPlayerId(long telegramId);
}