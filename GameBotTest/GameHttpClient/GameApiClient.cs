using System.Text;
using System.Net.Http;
using GameAPI.Models;
using Newtonsoft.Json;

namespace GameBotTest.GameHttpClient;

public class GameApiClient(HttpClient httpClient) : IGameApiClient
{

    public async Task<Player?> RegisterPlayerAsync(long telegramId, string name, string referrerId = null)
    {
        var playerData = new
        {
            telegramId,
            name,
            level = 0,
            score = 0,
            rating = 0,
            softCurrency = 0,
            hardCurrency = 0,
            referrerId
        };
        
        using var response = await httpClient.PostAsJsonAsync("/api/players", playerData);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Player>() : null;
    }
}