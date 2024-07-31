using GameAPI.Models;

namespace GameBotTest.GameHttpClient;

public interface IGameApiClient
{
    Task<Player?> RegisterPlayerAsync(long telegramId, string name, string referrerId = null);
}