using Matchmaker.Interfaces;
using Newtonsoft.Json;

namespace Matchmaker.ApiClients;

public class ApiClient(HttpClient httpClient) : IApiClient
{
    public async Task<string?> GetPlayerRegionIpAsync(long playerId)
    {
        var response = await httpClient.GetAsync($"http://gameapi:8080/api/players/ip/{playerId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var playerIpResponse = await response.Content.ReadFromJsonAsync<PlayerIpResponse>();
        return playerIpResponse?.RegionIp;
    }

    public async Task<bool> UpdatePlayerRatingAsync(long telegramId, int ratingChange)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"http://gameapi:8080/api/players/{telegramId}/rating", 
            ratingChange);

        return response.IsSuccessStatusCode;
    }
}

public class PlayerIpResponse
{
    public string RegionIp { get; set; }
}
