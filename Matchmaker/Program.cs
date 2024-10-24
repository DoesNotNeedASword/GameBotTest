using GameDomain.Models;
using Matchmaker.ApiClients;
using Matchmaker.Interfaces;
using Matchmaker.Models.Records;
using Matchmaker.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddHttpClient<IEdgegapService, EdgegapService>();
builder.Services.AddHttpClient<IApiClient, ApiClient>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

const int baseLobbyId = 1000000; 
var lobbies = new ConcurrentDictionary<long, Lobby>();
const int maxPlayers = 2;

app.MapHub<LobbyHub>("/lobby/hub");

app.MapPost("/lobby/create", async (CreateLobbyRequest request, IApiClient apiClient) =>
{
    var existingLobby = lobbies.Values.FirstOrDefault(lobby =>
        lobby.Players.Any(player => player.TelegramId == request.Creator.TelegramId) ||
        lobby.Spectators.Any(spectator => spectator.TelegramId == request.Creator.TelegramId)
    );

    if (existingLobby != null)
    {
        return Results.Ok(existingLobby);
    }

    var playerIp = await apiClient.GetPlayerRegionIpAsync(request.Creator.TelegramId);
    var lobbyId = GenerateLobbyId();

    if (string.IsNullOrEmpty(playerIp))
    {
        return Results.Problem("Failed to retrieve player's IP.");
    }

    var lobby = new Lobby(lobbyId, playerIp, request.Creator, request.LobbyName, request.Password);
    lobbies[lobbyId] = lobby;

    return Results.Ok(lobby);
});

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

app.MapPost("/lobby/{lobbyId:long}/join", async (long lobbyId, JoinLobbyRequest request, IApiClient apiClient, IHubContext<LobbyHub> hubContext) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby))
        return Results.NotFound("Lobby not found");

    if (lobby.Password != null && lobby.Password != request.Password)
        return Results.BadRequest("Incorrect password");

    if (lobby.Players.Count >= maxPlayers)
        return Results.BadRequest("Lobby is full. Cannot add more players.");

    var playerIp = await apiClient.GetPlayerRegionIpAsync(request.Player.TelegramId);

    if (string.IsNullOrEmpty(playerIp))
    {
        return Results.Problem("Failed to retrieve player's IP.");
    }

    lobby.Players.Add(request.Player);
    lobby.IpList.Add(playerIp); 
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveMessage", $"{request.Player.Name} has joined the lobby as a player.");
    
    return Results.Ok(lobby);
});

app.MapPost("/lobby/{lobbyId:long}/spectate", async (long lobbyId, JoinLobbyRequest request, IHubContext<LobbyHub> hubContext) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby))
        return Results.NotFound("Lobby not found");

    if (lobby.Password != null && lobby.Password != request.Password)
        return Results.BadRequest("Incorrect password");

    lobby.Spectators.Add(request.Player);
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveMessage", $"{request.Player.Name} has joined the lobby as a spectator.");

    return Results.Ok(lobby);
});

app.MapPost("/lobby/{lobbyId:long}/start", async (long lobbyId, IEdgegapService edgegapService, IHubContext<LobbyHub> hubContext) =>
{
    if (!lobbies.TryGetValue(lobbyId, out var lobby))
        return Results.NotFound("Lobby not found");

    if (lobby.Players.Count != 2)
        return Results.BadRequest("Lobby must have exactly 2 players to start the game");

    var result = await StartConnectionAttempt(lobbyId, lobby, edgegapService, lobby.IpList);
    
    if (result != null)
    {
        await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveMessage", "Game started");
        return Results.Ok("Game started");
    }

    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveMessage", "Failed to start the game.");
    return Results.Problem("Failed to start game");
});

app.MapPost("/lobby/leave", async (LeaveLobbyRequest request, IHubContext<LobbyHub> hubContext) =>
{
    if (!lobbies.TryGetValue(request.LobbyId, out var lobby)) 
        return Results.NotFound("Lobby not found");

    var player = lobby.Players.FirstOrDefault(p => p.TelegramId == request.PlayerId);
    if (player != null)
    {
        lobby.Players.Remove(player);
        await hubContext.Clients.Group(request.LobbyId.ToString()).SendAsync("ReceiveMessage", $"{player.Name} has left the lobby as a player.");
    }
    else
    {
        var spectator = lobby.Spectators.FirstOrDefault(p => p.TelegramId == request.PlayerId);
        if (spectator != null)
        {
            lobby.Spectators.Remove(spectator);
            await hubContext.Clients.Group(request.LobbyId.ToString()).SendAsync("ReceiveMessage", $"{spectator.Name} has left the lobby as a spectator.");
        }
        else
        {
            return Results.BadRequest("Player not found in the lobby");
        }
    }

    if (lobby.Players.Count != 0 || lobby.Spectators.Count != 0) return Results.Ok("Player left the lobby");
    await CloseLobby(request.LobbyId);
    return Results.Ok("Last player left the lobby, lobby is closed.");

});

app.MapPost("/lobby/notify/{lobbyId:long}", async (long lobbyId, [FromBody] string message, IHubContext<LobbyHub> hubContext) =>
{
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveMessage", message);
    return Results.Ok();
});

app.MapPost("/lobby/close/{lobbyId:long}", async (long lobbyId, IHubContext<LobbyHub> hubContext) =>
{
    await CloseLobby(lobbyId);
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveMessage", "Lobby has been closed.");
    return Results.Ok("Lobby closed successfully.");
});

app.Run();

long GenerateLobbyId()
{
    return !lobbies.IsEmpty ? lobbies.Last().Key + 1 : baseLobbyId;
}

async Task CloseLobby(long lobbyId)
{
    if (lobbies.TryRemove(lobbyId, out _))
    {
        await Task.CompletedTask;
    }
}

async Task<string?> StartConnectionAttempt(long lobbyId, Lobby lobby, IEdgegapService edgegapService, List<string> ipList)
{
    var requestId = await edgegapService.StartEdgegapServer(lobby, ipList);
    if (requestId is null) return null;
    
    for (var attempt = 0; attempt < 10; attempt++) 
    {
        var (serverAddress, serverPort) = await edgegapService.GetEdgegapServerStatus(requestId);

        if (serverAddress != null && serverPort != null)
        {
            return $"{serverAddress}:{serverPort}";
        }

        await Task.Delay(2000);
    }
    
    return null;
}
