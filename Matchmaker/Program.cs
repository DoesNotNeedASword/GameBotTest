using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using GameDomain.Models;
using Matchmaker.Interfaces;
using Matchmaker.Models.Dto;
using Matchmaker.Models.Records;
using Matchmaker.Models.Requests;
using Matchmaker.Services;
using Newtonsoft.Json;

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
// TODO: make id with redis
const int baseLobbyId = 1000000; // just a cool number for id
var lobbies = new ConcurrentDictionary<long, Lobby>();
// TODO: PLEASE LET ME USE RMQ!!!!!!! Update: No, RMQ is not working in unity webgl build :(
var lobbyConnections = new ConcurrentDictionary<long, ConcurrentDictionary<long, WebSocket>>(); // that's sucks...
const int maxPlayers = 2;


app.MapPost("/lobby/create", (CreateLobbyRequest request) =>
{
    var existingLobby = lobbies.Values.FirstOrDefault(lobby => 
        lobby.Players.Any(player => player.TelegramId == request.Creator.TelegramId) || 
        lobby.Spectators.Any(spectator => spectator.TelegramId == request.Creator.TelegramId)
    );

    if (existingLobby != null)
    {
        return Results.Ok(existingLobby);
    }

    var lobbyId = GenerateLobbyId();
    var lobby = new Lobby(lobbyId, request.Creator, request.LobbyName, request.Password);
    lobbies[lobbyId] = lobby;
    lobbyConnections[lobbyId] = new ConcurrentDictionary<long, WebSocket>(); 
    return Results.Ok(lobby);
});


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

    if (lobby.Players.Count >= maxPlayers) return Results.BadRequest("Lobby is full. Cannot add more players.");
    
    lobby.Players.Add(request.Player);
    
    await NotifyLobby(lobbyId, LobbyNotificationStatus.PlayerConnected, $"{request.Player.Name} has joined the lobby as a player.");
    return Results.Ok(lobby);
});


app.MapPost("/lobby/{lobbyId:long}/spectate", async (long lobbyId, JoinLobbyRequest request) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby))
        return Results.NotFound("Lobby not found");

    if (lobby.Password != null && lobby.Password != request.Password)
        return Results.BadRequest("Incorrect password");

    lobby.Spectators.Add(request.Player);
    
    await NotifyLobby(lobbyId, LobbyNotificationStatus.PlayerConnected, $"{request.Player.Name} has joined the lobby as a spectator.");
    return Results.Ok(lobby);
});


app.MapPost("/lobby/{lobbyId:long}/start", async (long lobbyId, IEdgegapService edgegapService) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby)) 
        return Results.NotFound("Lobby not found");
    
    if (lobby.Players.Count != 2) 
        return Results.BadRequest("Lobby must have exactly 2 players to start the game");

    var result = await StartConnectionAttempt(lobbyId, lobby, edgegapService);

    if (result != null)
        return Results.Ok("Game started");

    await NotifyLobby(lobbyId, LobbyNotificationStatus.WebSocketError, "Failed to start the game.");
    return Results.Problem("Failed to start game");
});

app.MapPost("/lobby/leave", async (LeaveLobbyRequest request) =>
{
    if (!lobbies.TryGetValue(request.LobbyId, out var lobby)) 
        return Results.NotFound("Lobby not found");

    var player = lobby.Players.FirstOrDefault(p => p.TelegramId == request.PlayerId);
    if (player != null)
    {
        lobby.Players.Remove(player);
        await NotifyLobby(request.LobbyId, LobbyNotificationStatus.PlayerDisconnected, $"{player.Name} has left the lobby as a player.");
    }
    else
    {
        var spectator = lobby.Spectators.FirstOrDefault(p => p.TelegramId == request.PlayerId);
        if (spectator != null)
        {
            lobby.Spectators.Remove(spectator);
            await NotifyLobby(request.LobbyId, LobbyNotificationStatus.PlayerDisconnected, $"{spectator.Name} has left the lobby as a spectator.");
        }
        else
        {
            return Results.BadRequest("Player not found in the lobby");
        }
    }

    if (lobby.Players.Count == 0 && lobby.Spectators.Count == 0)
    {
        await CloseLobby(request.LobbyId);
        return Results.Ok("Last player left the lobby, lobby is closed.");
    }

    if (!lobbyConnections.TryGetValue(request.LobbyId, out var playerConnections) ||
        !playerConnections.TryGetValue(request.PlayerId, out var socket)) return Results.Ok("Player left the lobby");
    
    playerConnections.Remove(request.PlayerId, out _);
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

app.MapGet("/lobby/ws/{lobbyId:long}&{playerId:long}", async (long lobbyId, long playerId, HttpContext context) =>
{
    if (!lobbies.ContainsKey(lobbyId))
    {
        context.Response.StatusCode = 404;
        var notificationDto = new LobbyNotificationDto((int)LobbyNotificationStatus.LobbyNotFound, "Lobby not found");
        await context.Response.WriteAsJsonAsync(notificationDto);
        return;
    }

    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var playerConnections = lobbyConnections.GetOrAdd(lobbyId, _ => new ConcurrentDictionary<long, WebSocket>());

        playerConnections[playerId] = socket;

        var connectionNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerConnected, $"Player {playerId} connected to Lobby {lobbyId}");
        var connectionMessage = JsonConvert.SerializeObject(connectionNotification);
        var buffer = Encoding.UTF8.GetBytes(connectionMessage);
        await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

        await Receive(socket, playerId, lobbyId, async (result, _) =>
        {
            if (result.MessageType == WebSocketMessageType.Close)
            {
                playerConnections.Remove(playerId, out var _);

                var disconnectionNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerDisconnected, $"Player {playerId} disconnected from Lobby {lobbyId}");
                var disconnectionMessage = JsonConvert.SerializeObject(disconnectionNotification);
                var disconnectionBuffer = Encoding.UTF8.GetBytes(disconnectionMessage);
                await socket.SendAsync(new ArraySegment<byte>(disconnectionBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocket client", CancellationToken.None);
            }
        });
    }
    else
    {
        context.Response.StatusCode = 400;
        var notificationDto = new LobbyNotificationDto((int)LobbyNotificationStatus.InvalidRequest, "Invalid WebSocket request");
        await context.Response.WriteAsJsonAsync(notificationDto);
    }
});


app.MapPost("/lobby/close/{lobbyId:long}", async (long lobbyId) =>
{
    await CloseLobby(lobbyId);
    return Results.Ok("Lobby closed successfully.");
});



app.MapPost("/lobby/closeGame", async (CloseGameRequest request, IEdgegapService edgegapService, HttpClient httpClient) =>
{
    var result = await edgegapService.StopDeployment(request.RequestId);
    var lobby = lobbies.FirstOrDefault(l => l.Value.Players.Any(p => p.TelegramId == request.Winner));
    await CloseLobby(lobby.Key);
    var winnerResponse = await httpClient.PutAsJsonAsync(
        $"http://gameapi:8080/api/players/{request.Winner}/rating", 
        1
    );

    if (!winnerResponse.IsSuccessStatusCode)
    {
        return Results.Problem($"Failed to update rating for the winner with Telegram ID {request.Winner}.");
    }

    foreach (var loser in request.Losers)
    {
        var loserResponse = await httpClient.PutAsJsonAsync(
            $"http://gameapi:8080/api/players/{loser}/rating", 
            -1
        );

        if (!loserResponse.IsSuccessStatusCode)
        {
            return Results.Problem($"Failed to update rating for player with Telegram ID {loser}.");
        }
    }

    return Results.Ok(new { Winner = request.Winner, Losers = request.Losers });
});

app.Run();
return;

async Task NotifyLobby(long lobbyId, LobbyNotificationStatus notificationStatus, string additionalMessage = "")
{
    if (lobbyConnections.TryGetValue(lobbyId, out var playerConnections))
    {
        var tasks = playerConnections.Values.Select(async socket =>
        {
            LobbyNotificationDto notificationDto;

            try
            {
                notificationDto = new LobbyNotificationDto((int)notificationStatus, additionalMessage);
                var serializedMessage = JsonConvert.SerializeObject(notificationDto); 
                var buffer = Encoding.UTF8.GetBytes(serializedMessage); 

                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                notificationDto = new LobbyNotificationDto((int)LobbyNotificationStatus.WebSocketError, "Error while NotifyLobby");
                var serializedMessage = JsonConvert.SerializeObject(notificationDto);
                var buffer = Encoding.UTF8.GetBytes(serializedMessage);

                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Error while NotifyLobby", CancellationToken.None);
            }
            Console.WriteLine($"Notification Sent: StatusCode = {notificationDto.StatusCode}, Message = {notificationDto.Message}");
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
            await NotifyLobby(lobbyId, LobbyNotificationStatus.GameStarted, $"{serverAddress}:{serverPort}");
            return $"{serverAddress}:{serverPort}";
        }

        await Task.Delay(2000);
    }
    return null;
}

long GenerateLobbyId()
{
    return !lobbies.IsEmpty ? lobbies.Last().Key + 1 : baseLobbyId;
}

async Task Receive(WebSocket socket, long playerId, long lobbyId, Func<WebSocketReceiveResult, byte[], Task> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(1));

        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Timeout", CancellationToken.None);
            break;
        }
        
        var result = await receiveTask;
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await LeaveLobby(lobbyId, playerId);
            break;
        }

        await handleMessage(result, buffer);
    }
}

async Task LeaveLobby(long lobbyId, long playerId)
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby)) 
        return; 

    var player = lobby.Players.FirstOrDefault(p => p.TelegramId == playerId);
    if (player != null)
    {
        lobby.Players.Remove(player);
        await NotifyLobby(lobbyId, LobbyNotificationStatus.PlayerDisconnected, $"{player.Name} has left the lobby.");
    }
    else
    {
        var spectator = lobby.Spectators.FirstOrDefault(p => p.TelegramId == playerId);
        if (spectator != null)
        {
            lobby.Spectators.Remove(spectator);
            await NotifyLobby(lobbyId, LobbyNotificationStatus.PlayerDisconnected, $"{spectator.Name} has left the lobby.");
        }
        else
        {
            return; 
        }
    }

    if (lobby.Players.Count == 0 && lobby.Spectators.Count == 0)
    {
        await CloseLobby(lobbyId); 
    }
    else
    {
        if (lobbyConnections.TryGetValue(lobbyId, out var playerConnections))
        {
            playerConnections.Remove(playerId, out var socket);
            if (socket != null)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Player left", CancellationToken.None);
            }
        }
    }
}

async Task CloseLobby(long lobbyId)
{
    if (lobbies.TryRemove(lobbyId, out _))
    {
        await NotifyLobby(lobbyId, LobbyNotificationStatus.LobbyClosed, "The lobby has been closed.");

        if (lobbyConnections.TryRemove(lobbyId, out var playerConnections))
        {
            foreach (var connection in playerConnections.Values)
            {
                await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Lobby closed", CancellationToken.None);
            }
        }
    }
}

