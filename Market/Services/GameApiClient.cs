using System.Text;
using GameDomain.Models;
using GameDomain.Models.DTOs;
using Market.Interfaces;
using Newtonsoft.Json;

namespace Market.Services;

public class GameApiClient : IGameApiClient
{
    private readonly HttpClient _httpClient;
    public GameApiClient(HttpClient httpClient, IConfiguration configuration) 
    {
        var baseUri = configuration["GameApi"];
        if (string.IsNullOrEmpty(baseUri))
        {
            throw new ArgumentException("Base URI for Game API is not configured in environment variables.");
        }
        httpClient.BaseAddress = new Uri(baseUri);
        _httpClient = httpClient;
    }


    public async Task<Player?> GetPlayerAsync(long playerId)
    {
        var response = await _httpClient.GetAsync($"/api/players/{playerId}");
        return response.IsSuccessStatusCode
            ? JsonConvert.DeserializeObject<Player>(await response.Content.ReadAsStringAsync())
            : null;
    }

    public async Task<bool> UpdatePlayerAsync(Player player)
    {
        var content = new StringContent(JsonConvert.SerializeObject(player), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"/api/players/{player.TelegramId}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TransferCarAsync(string carId, long newOwnerId)
    {
        var response = await _httpClient.PutAsync($"/api/cars/transfer", 
            new StringContent(JsonConvert.SerializeObject(new TransferCarDto(carId, newOwnerId)), Encoding.UTF8, "application/json"));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TransferCurrencyAsync(long buyerId, long sellerId, int amount)
    {
        var transferDto = new TransferCurrencyDto
        {
            FromPlayerId = buyerId,
            ToPlayerId = sellerId,
            Amount = amount
        };

        var response = await _httpClient.PostAsJsonAsync("api/players/transfer", transferDto);

        return response.IsSuccessStatusCode;
    }
}