using Matchmaker.Models.Dto;

namespace Matchmaker.Services;

using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class LobbyHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var lobbyId = Context.GetHttpContext()?.Request.Query["lobbyId"].ToString();

        if (!string.IsNullOrEmpty(lobbyId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
            await SendLobbyNotification(lobbyId, new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerConnected, $"Player joined lobby {lobbyId}"));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var lobbyId = Context.GetHttpContext()?.Request.Query["lobbyId"].ToString();

        if (!string.IsNullOrEmpty(lobbyId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
            await SendLobbyNotification(lobbyId, new LobbyNotificationDto((int)LobbyNotificationStatus.PlayerDisconnected, $"Player left lobby {lobbyId}"));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendLobbyNotification(string lobbyId, LobbyNotificationDto notification)
    {
        await Clients.Group(lobbyId).SendAsync("ReceiveNotification", notification);
    }

    public async Task SendLobbyMessage(string lobbyId, string message)
    {
        await SendLobbyNotification(lobbyId, new LobbyNotificationDto((int)LobbyNotificationStatus.WebSocketError, message));
    }
}

