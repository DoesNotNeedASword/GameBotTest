using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameDomain.Models;
using Matchmaker;
using Matchmaker.Models.Response;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseWebSockets();
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    Converters = { new CustomDateTimeConverter() }
};

var lobbies = new ConcurrentDictionary<long, Lobby>();
var lobbyConnections = new ConcurrentDictionary<long, List<WebSocket>>();
var edgegapApiToken = builder.Configuration["EDGEGAP_API_TOKEN"];
var dockerImage = builder.Configuration["DOCKER_IMAGE"];
var edgegapVersion = builder.Configuration["VERSION"];

app.MapPost("/lobby/create", (CreateLobbyRequest request) =>
{
    var lobbyId = request.Creator.TelegramId;
    var lobby = new Lobby(lobbyId, request.Creator, request.LobbyName, request.Password);
    lobbies[lobbyId] = lobby;
    lobbyConnections[lobbyId] = [];
    return Results.Ok(lobby);
});

//TODO MVP1: get lobby by id, get first or default 
//TODO MVP2: spectators, bets, avatars

app.MapGet("lobby/any", () =>
{
    var lobby = lobbies.FirstOrDefault(l => l.Value.Players.Count == 1);
    return Results.Ok(lobby.Value);
});

app.MapGet("lobby/{id:long}", (long id) =>
{
    var lobby = lobbies.FirstOrDefault(l => l.Key == id);
    return Results.Ok(lobby.Value);
});

app.MapPost("/lobby/{lobbyId:long}/join", async (long lobbyId, JoinLobbyRequest request) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby))
        return Results.NotFound("Lobby not found");

    if (lobby.Password != null && lobby.Password != request.Password)
        return Results.BadRequest("Incorrect password");

    if (lobby.Players.Count >= 2) return Results.BadRequest("Lobby is full. Cannot add more players.");
    lobby.Players.Add(request.Player);
    await NotifyLobby(lobbyId, $"{request.Player.Name} has joined the lobby as a player.");
    return Results.Ok(lobby);

});

app.MapPost("/lobby/{lobbyId:long}/spectate", async (long lobbyId, JoinLobbyRequest request) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby))
        return Results.NotFound("Lobby not found");

    if (lobby.Password != null && lobby.Password != request.Password)
        return Results.BadRequest("Incorrect password");

    lobby.Spectators.Add(request.Player);
    await NotifyLobby(lobbyId, $"{request.Player.Name} has joined the lobby as a spectator.");
    return Results.Ok(lobby);
});


app.MapPost("/lobby/{lobbyId:long}/start", async (long lobbyId) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby)) return Results.NotFound("Lobby not found");
    if (lobby.Players.Count != 2) return Results.BadRequest("Lobby must have exactly 2 players to start the game");

    var result = await StartConnectionAttempt(lobbyId, lobby);

    return result != null ? Results.Ok("Game started") : Results.Problem("Failed to start game");
});

app.MapPost("/lobby", (string? filter = null) =>
{
    var result = string.IsNullOrEmpty(filter) 
        ? lobbies.Values.ToList() 
        : lobbies.Values.Where(lobby => lobby.LobbyName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    return Results.Ok(result);
});

app.MapGet("/lobby/ws/{lobbyId:long}", async (long lobbyId, HttpContext context) =>
{
    if (!lobbies.ContainsKey(lobbyId))
    {
        context.Response.StatusCode = 404;
        return;
    }

    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        lobbyConnections[lobbyId].Add(socket);

        await Receive(socket, async (result, buffer) =>
        {
            if (result.MessageType != WebSocketMessageType.Close) return;
            lobbyConnections[lobbyId].Remove(socket);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocket client", CancellationToken.None);
        });
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run();
return;

async Task NotifyLobby(long lobbyId, string message)
{
    if (lobbyConnections.TryGetValue(lobbyId, out var sockets))
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var tasks = sockets.Select(async socket =>
        {
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Error while NotifyLobby",
                    cancellationToken: new CancellationToken());
            }
        });
 
        await Task.WhenAll(tasks);
    }
}


async Task<string> StartEdgegapServer(Lobby lobby)
{
    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("authorization", edgegapApiToken);

        var requestBody = new
        {
            app_name = dockerImage,
            app_version = edgegapVersion,
            ip_list = new[] { "1.2.3.4" },
            // idc what all of these params below are doing
        };

        var response = await client.PostAsJsonAsync("https://api.edgegap.com/v1/deploy", requestBody);
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<EdgegapCreateResponse>();
        return responseData!.RequestId;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return string.Empty;
    }
}

async Task<(string? Dns, int? ExternalPort)> GetEdgegapServerStatus(string requestId)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("authorization", edgegapApiToken);

    var response = await client.GetAsync($"https://api.edgegap.com/v1/status/{requestId}");
    response.EnsureSuccessStatusCode();
    var responseData = await response.Content.ReadFromJsonAsync<EdgegapStatusResponse>(options); // mfs from edgegap don't know that iso and timestamp exist so I have to use custom converter 
    if (responseData?.Running != true) return (null, null);
    var externalPort = responseData.Ports["Game Port"].External;
    var dns = responseData.Fqdn;
    return (dns, externalPort);

}
async Task<string?> StartConnectionAttempt(long lobbyId, Lobby lobby)
{
    var requestId = await StartEdgegapServer(lobby);

    var isServerReady = false;
    while (!isServerReady)
    {
        var (serverAddress, serverPort) = await GetEdgegapServerStatus(requestId);

        if (serverAddress != null && serverPort != null)
        {
            isServerReady = true;
            await NotifyLobby(lobbyId, $"{serverAddress}:{serverPort}");

            return $"{serverAddress}:{serverPort}";
        }
        await Task.Delay(2000);
    }

    return null;
}


async Task Receive(WebSocket socket, Func<WebSocketReceiveResult, byte[], Task> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2)); // Таймаут 2 минуты
        
        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            // Таймаут
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Timeout", CancellationToken.None);
            break;
        }
        
        var result = await receiveTask;
        await handleMessage(result, buffer);
    }
}
public record CreateLobbyRequest(Player Creator, string LobbyName, string? Password);
public record JoinLobbyRequest(Player Player, string? Password);

public class Lobby
{
    public long Id { get; }
    public string LobbyName { get; }
    public string? Password { get; }
    public List<Player> Players { get; } = new();
    public List<Player> Spectators { get; } = new();

    public Lobby(long id, Player creator, string lobbyName, string? password)
    {
        Id = id;
        LobbyName = lobbyName;
        Password = password;
        Players.Add(creator);
    }
}

