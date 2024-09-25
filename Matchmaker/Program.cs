using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using GameDomain.Models;
using Matchmaker.Interfaces;
using Matchmaker.Models.Records;
using Matchmaker.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IEdgegapService, EdgegapService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseWebSockets();

var lobbies = new ConcurrentDictionary<long, Lobby>();
var lobbyConnections = new ConcurrentDictionary<long, List<WebSocket>>();


app.MapPost("/lobby/create", (CreateLobbyRequest request) =>
{
    var lobbyId = GenerateLobbyId(); // Генерация уникального ID для лобби
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

app.MapGet("lobby/creator/{id:long}", (long id) =>
{
    var lobby = lobbies.FirstOrDefault(l => l.Value.Players.FirstOrDefault()!.TelegramId == id);
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

app.MapPost("/lobby/{lobbyId:long}/start", async (long lobbyId, IEdgegapService edgegapService) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby)) return Results.NotFound("Lobby not found");
    if (lobby.Players.Count != 2) return Results.BadRequest("Lobby must have exactly 2 players to start the game");

    var result = await StartConnectionAttempt(lobbyId, lobby, edgegapService);

    return result != null ? Results.Ok("Game started") : Results.Problem("Failed to start game");
});

app.MapPost("/lobby/leave", async (LeaveLobbyRequest request) =>
{
    if (!lobbies.TryGetValue(request.LobbyId, out var lobby)) 
        return Results.NotFound("Lobby not found");

    var player = lobby.Players.FirstOrDefault(p => p.TelegramId == request.PlayerId);
    if (player != null)
    {
        lobby.Players.Remove(player);
        await NotifyLobby(request.LobbyId, $"{request.PlayerId} has left the lobby as a player.");
    }
    else
    {
        var spectator = lobby.Spectators.FirstOrDefault(p => p.TelegramId == request.PlayerId);
        if (spectator != null)
        {
            lobby.Spectators.Remove(spectator);
            await NotifyLobby(request.LobbyId, $"{request.PlayerId} has left the lobby as a spectator.");
        }
        else
        {
            return Results.BadRequest("Player not found in the lobby");
        }
    }
    if (!lobbyConnections.TryGetValue(request.LobbyId, out var sockets)) return Results.Ok("Player left the lobby");
    var socket = sockets.FirstOrDefault(s => s.State == WebSocketState.Open); 
    if (socket is null) return Results.Ok("Player left the lobby");
    sockets.Remove(socket);
    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Player left", CancellationToken.None);

    return Results.Ok("Player left the lobby");
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

        await Receive(socket, async (result, _) =>
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

async Task<string?> StartConnectionAttempt(long lobbyId, Lobby lobby, IEdgegapService edgegapService)
{
    var requestId = await edgegapService.StartEdgegapServer(lobby);
    if (requestId is null) return null;
    for (var attempt = 0; attempt < 10; attempt++) 
    {
        var (serverAddress, serverPort) = await edgegapService.GetEdgegapServerStatus(requestId);

        if (serverAddress != null && serverPort != null)
        {
            await NotifyLobby(lobbyId, $"{serverAddress}:{serverPort}");
            return $"{serverAddress}:{serverPort}";
        }

        await Task.Delay(1000);
    }
    return null;
}

long GenerateLobbyId()
{
    using var rng = RandomNumberGenerator.Create();
    var randomNumber = new byte[8]; // 8 байт = 64 бита, для long
    rng.GetBytes(randomNumber);
    return BitConverter.ToInt64(randomNumber, 0) & long.MaxValue; // Убираем отрицательные значения
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
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Timeout", CancellationToken.None);
            break;
        }
        
        var result = await receiveTask;
        await handleMessage(result, buffer);
    }
}