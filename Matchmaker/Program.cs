using GameDomain.Models;
using Matchmaker.ApiClients;
using Matchmaker.Interfaces;
using Matchmaker.Models.Dto;
using Matchmaker.Models.Records;
using Matchmaker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IEdgegapService, EdgegapService>();
builder.Services.AddHttpClient<IApiClient, ApiClient>();
var redisConfiguration = builder.Configuration["REDIS_CONNECTIONSTRING"]!;
var multiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddSignalR().AddStackExchangeRedis(redisConfiguration); 
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConfiguration;
    options.InstanceName = "LobbyInstance";
});
builder.Services.AddScoped<ILobbyCacheService, LobbyCacheService>();

const int maxPlayers = 2;

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseWebSockets();

app.MapHub<LobbyHub>("/lobby/hub");

app.MapPost("/lobby/create", async (CreateLobbyRequest request, IApiClient apiClient, ILobbyCacheService lobbyCacheService) =>
{
    var playerLobbyKey = $"playerLobby:{request.Creator.TelegramId}";
    var existingLobbyId = await lobbyCacheService.GetPlayerLobbyAsync(playerLobbyKey);

    if (existingLobbyId.HasValue)
    {
        var existingLobby = await lobbyCacheService.GetLobbyAsync(existingLobbyId.Value);
        return Results.Ok(existingLobby);
    }

    var playerIp = await apiClient.GetPlayerRegionIpAsync(request.Creator.TelegramId);
    var lobbyId = await lobbyCacheService.GenerateNewLobbyIdAsync();

    if (string.IsNullOrEmpty(playerIp))
    {
        return Results.Problem("Failed to retrieve player's IP.");
    }

    var lobby = new Lobby(lobbyId, playerIp, request.Creator, request.LobbyName, request.Password);
    await lobbyCacheService.SaveLobbyAsync(lobby);
    await lobbyCacheService.SetPlayerLobbyAsync(playerLobbyKey, lobbyId);

    return Results.Ok(lobby);
});

app.MapPost("/lobby/{lobbyId:long}/join", async (long lobbyId, JoinLobbyRequest request, IApiClient apiClient, IHubContext<LobbyHub> hubContext, ILobbyCacheService lobbyCacheService) =>
{
    var lobby = await lobbyCacheService.GetLobbyAsync(lobbyId);
    if (lobby == null) return Results.NotFound("Lobby not found");

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
    await lobbyCacheService.SaveLobbyAsync(lobby);
    await lobbyCacheService.UpdateLobbyPlayerCountAsync(lobbyId, lobby.Players.Count);
    
    var notification = new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerConnected, $"{request.Player.Name} has joined the lobby as a player.");
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(notification));
    
    return Results.Ok(lobby);
});

app.MapGet("lobby/any", async (ILobbyCacheService lobbyCacheService) =>
{
    var availableLobby = await lobbyCacheService.FindLobbyWithSinglePlayerAsync();
    return availableLobby != null ? Results.Ok(availableLobby) : Results.NotFound("No available lobby found");
});

app.MapPost("/lobby", async (string? filter, ILobbyCacheService lobbyCacheService) =>
{
    var result = await lobbyCacheService.GetAllLobbiesAsync(filter);
    return Results.Ok(result);
});

app.MapGet("lobby/{id:long}", async (long id, ILobbyCacheService lobbyCacheService) =>
{
    var lobby = await lobbyCacheService.GetLobbyAsync(id);
    return lobby != null ? Results.Ok(lobby) : Results.NotFound("Lobby not found");
});

app.MapGet("lobby/creator/{id:long}", async (long id, ILobbyCacheService lobbyCacheService) =>
{
    var lobby = await lobbyCacheService.GetLobbyByCreatorIdAsync(id); 
    return lobby != null ? Results.Ok(lobby) : Results.NotFound("Lobby not found for this creator");
});

app.MapPost("/lobby/{lobbyId:long}/spectate", async (long lobbyId, JoinLobbyRequest request, IHubContext<LobbyHub> hubContext, ILobbyCacheService lobbyCacheService) =>
{
    var lobby = await lobbyCacheService.GetLobbyAsync(lobbyId);
    if (lobby == null) return Results.NotFound("Lobby not found");

    if (lobby.Password != null && lobby.Password != request.Password)
        return Results.BadRequest("Incorrect password");

    lobby.Spectators.Add(request.Player);
    await lobbyCacheService.SaveLobbyAsync(lobby);

    var notification = new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerConnected, $"{request.Player.Name} has joined the lobby as a spectator.");
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(notification));

    return Results.Ok(lobby);
});

app.MapPost("/lobby/{lobbyId:long}/start", async (long lobbyId, IEdgegapService edgegapService, ILobbyCacheService lobbyCacheService, IHubContext<LobbyHub> hubContext) =>
{
    var lobby = await lobbyCacheService.GetLobbyAsync(lobbyId);
    if (lobby == null) return Results.NotFound("Lobby not found");

    if (lobby.Players.Count != maxPlayers)
        return Results.BadRequest("Lobby must have exactly 2 players to start the game");

    var startNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.GameIsStarting, "Game is starting...");
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(startNotification));

    var connectionDetails = await StartConnectionAttempt(lobbyId, lobby, edgegapService, lobby.IpList);
    if (connectionDetails != null)
    {
        var gameStartedNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.GameStarted, "Game started");
        await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(gameStartedNotification));
        return Results.Ok("Game started");
    }

    var errorNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.WebSocketError, "Failed to start the game");
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(errorNotification));
    return Results.Problem("Failed to start game");
});

app.MapPost("/lobby/leave", async (LeaveLobbyRequest request, ILobbyCacheService lobbyCacheService, IHubContext<LobbyHub> hubContext) =>
{
    var lobby = await lobbyCacheService.GetLobbyAsync(request.LobbyId);
    if (lobby == null) return Results.NotFound("Lobby not found");

    var player = lobby.Players.FirstOrDefault(p => p.TelegramId == request.PlayerId);
    if (player == null) return Results.Ok("Player left the lobby");
    lobby.Players.Remove(player);
    await lobbyCacheService.UpdateLobbyPlayerCountAsync(request.LobbyId, lobby.Players.Count);
    await lobbyCacheService.SaveLobbyAsync(lobby);

    var leaveNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerDisconnected, $"{player.Name} has left the lobby");
    await hubContext.Clients.Group(request.LobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(leaveNotification));

    return Results.Ok("Player left the lobby");
});

app.MapPost("/lobby/notify/{lobbyId:long}", async (long lobbyId, [FromBody] string message, IHubContext<LobbyHub> hubContext) =>
{
    var notification = new LobbyNotificationDto((int)LobbyNotificationStatus.Heartbeat, message);
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(notification));
    
    return Results.Ok();
});

app.MapPost("/lobby/close/{lobbyId:long}", async (long lobbyId, ILobbyCacheService lobbyCacheService, IHubContext<LobbyHub> hubContext) =>
{
    await lobbyCacheService.DeleteLobbyAsync(lobbyId);
    
    var closeNotification = new LobbyNotificationDto((int)LobbyNotificationStatus.LobbyClosed, "Lobby has been closed.");
    await hubContext.Clients.Group(lobbyId.ToString()).SendAsync("ReceiveNotification", JsonConvert.SerializeObject(closeNotification));
    
    return Results.Ok("Lobby closed successfully.");
});

app.Run();
return;

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
