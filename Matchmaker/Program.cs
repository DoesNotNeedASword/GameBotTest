using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using GameDomain.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseWebSockets();

var lobbies = new ConcurrentDictionary<long, Lobby>();
var lobbyConnections = new ConcurrentDictionary<long, List<WebSocket>>();
var edgegapApiToken = builder.Configuration["EDGEGAP_API_TOKEN"];
var dockerImage = builder.Configuration["DOCKER_IMAGE"];

app.MapPost("/lobby/create", (CreateLobbyRequest request) =>
{
    var lobbyId = request.Creator.TelegramId;
    var lobby = new Lobby(lobbyId, request.Creator, request.LobbyName, request.Password);
    lobbies[lobbyId] = lobby;
    lobbyConnections[lobbyId] = [];
    return Results.Ok(lobby);
});

app.MapPost("/lobby/{lobbyId:long}/join", async (long lobbyId, JoinLobbyRequest request) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby)) return Results.NotFound("Lobby not found");
    if (lobby.Password != null && lobby.Password != request.Password)
    {
        return Results.BadRequest("Incorrect password");
    }

    if (lobby.Players.Count < 2)
    {
        lobby.Players.Add(request.Player);
        await NotifyLobby(lobbyId, $"{request.Player.Name} has joined the lobby as a player.");
        return Results.Ok(lobby);
    }
    else
    {
        lobby.Spectators.Add(request.Player);
        await NotifyLobby(lobbyId, $"{request.Player.Name} has joined the lobby as a spectator.");
        return Results.Ok(lobby);
    }
});

app.MapPost("/lobby/{lobbyId:long}/start", async (long lobbyId) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby)) return Results.NotFound("Lobby not found");
    if (lobby.Players.Count != 2) return Results.BadRequest("Lobby must have exactly 2 players to start the game");
    await StartEdgegapServer(lobby);
    await NotifyLobby(lobbyId, "Game started");
    return Results.Ok("Game started");
});

app.MapGet("/lobbies", (string? filter = null) =>
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


async Task StartEdgegapServer(Lobby lobby)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", edgegapApiToken);

    var requestBody = new
    {
        name = "game-session-" + lobby.Id,
        regions = new[] { "na-east" },
        configuration = new
        {
            image = dockerImage,
            ports = new[]
            {
                new
                {
                    container = 7777,
                    public_port = 7777
                }
            }
        }
    };

    var response = await client.PostAsJsonAsync("https://api.edgegap.com/deploy", requestBody);
    response.EnsureSuccessStatusCode();

    var responseData = await response.Content.ReadFromJsonAsync<EdgegapDeploymentResponse>();
    await NotifyLobby(lobby.Id, "Server deployed and game started");
}

async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
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

public class EdgegapDeploymentResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    // Добавьте другие необходимые поля
}
