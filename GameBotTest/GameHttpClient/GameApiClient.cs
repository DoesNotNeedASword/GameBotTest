using System.Text;
using System.Net.Http;
using GameDomain.Models;
using Newtonsoft.Json;

namespace GameBotTest.GameHttpClient;

public class GameApiClient(IHttpClientFactory httpClient) : IGameApiClient
{
    private readonly HttpClient _httpClient = httpClient.CreateClient("GameApi");

    public async Task<Player?> RegisterPlayerAsync(long telegramId, string name, string referrerId = null)
    {
        var playerData = new Player
        {
            TelegramId = telegramId,
            Name = name,
            ReferrerId = referrerId
        };
        
        using var response = await _httpClient.PostAsJsonAsync("/api/players", playerData);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Player>() : null;
    }

    public async Task<string> GetPlayerId(long telegramId)
    {
        using var response = await _httpClient.GetAsync($"/api/players/{telegramId}");
        return response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<Player>())!.Id : string.Empty;
    }
}