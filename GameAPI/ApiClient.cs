using GameDomain.Models;

namespace GameAPI 
{
    public class ApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiClient(string baseUrl, string authorizationToken = null)
        {
            _baseUrl = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
            _httpClient = new HttpClient();

            if (!string.IsNullOrEmpty(authorizationToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authorizationToken);
            }
        }

        public async Task<List<Player>> GetPlayersAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}api/players");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<Player>>();
        }

        public async Task<Player> GetPlayerAsync(long telegramId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}api/players/{telegramId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Player>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                throw new HttpRequestException($"Error getting player: {response.ReasonPhrase}");
            }
        }

        public async Task<Player> CreatePlayerAsync(Player player)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}api/players", player);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Player>();
        }

        public async Task<bool> UpdatePlayerAsync(long telegramId, Player player)
        {
            var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}api/players/{telegramId}", player);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdatePlayerRatingAsync(long telegramId, int ratingChange)
        {
            var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}api/players/{telegramId}/rating", ratingChange);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeletePlayerAsync(long telegramId)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}api/players/{telegramId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<List<Player>> GetLeadersAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}api/leaders");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<Player>>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<Player>();
            }
            else
            {
                throw new HttpRequestException($"Error getting leaders: {response.ReasonPhrase}");
            }
        }

        public async Task<bool> VerifyAsync(Dictionary<string, string> formData)
        {
            var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PostAsync($"{_baseUrl}api/verify", content);
            return response.IsSuccessStatusCode;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
