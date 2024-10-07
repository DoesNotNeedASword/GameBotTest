using System.Text.Json;
using GameDomain.Models;
using Matchmaker.Interfaces;
using Matchmaker.Models.Response;

namespace Matchmaker.Services;

public class EdgegapService : IEdgegapService
{
    private readonly HttpClient _httpClient;
    private readonly string? _dockerImage;
    private readonly string? _edgegapVersion;
    private readonly JsonSerializerOptions _jsonOptions;

    public EdgegapService(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        var edgegapApiToken = configuration["EDGEGAP_API_TOKEN"];
        _dockerImage = configuration["DOCKER_IMAGE"];
        _edgegapVersion = configuration["VERSION"];
        if (_dockerImage is null || _edgegapVersion is null) throw new ArgumentNullException();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new CustomDateTimeConverter() }
        };
        _httpClient.DefaultRequestHeaders.Add("authorization", edgegapApiToken);
    }

    public async Task<string?> StartEdgegapServer(Lobby lobby, List<string> ipList)
    {
        try
        {
            var requestBody = new
            {
                app_name = _dockerImage,
                app_version = _edgegapVersion,
                ip_list = ipList.ToArray()
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.edgegap.com/v1/deploy", requestBody);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadFromJsonAsync<EdgegapCreateResponse>(_jsonOptions);
            return responseData?.RequestId;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public async Task<(string? Dns, int? ExternalPort)> GetEdgegapServerStatus(string requestId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://api.edgegap.com/v1/status/{requestId}");
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadFromJsonAsync<EdgegapStatusResponse>(_jsonOptions);
            if (responseData?.Running != true) return (null, null);

            var externalPort = responseData.Ports["Game Port"].External;
            var dns = responseData.Fqdn;
            return (dns, externalPort);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return (null, null);
        }
    }
    public async Task<bool> StopDeployment(string requestId)
    {
        var response = await _httpClient.DeleteAsync($"https://api.edgegap.com/v1/stop/{requestId}");
        return response.IsSuccessStatusCode;
    }
}
