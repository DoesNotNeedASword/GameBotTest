namespace Matchmaker.Services;

using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class LobbyHub : Hub
{
    public async Task JoinLobby(string lobbyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        await Clients.Group(lobbyId).SendAsync("ReceiveMessage", $"Player joined lobby {lobbyId}");
    }

    public async Task LeaveLobby(string lobbyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
        await Clients.Group(lobbyId).SendAsync("ReceiveMessage", $"Player left lobby {lobbyId}");
    }

    public async Task SendLobbyMessage(string lobbyId, string message)
    {
        await Clients.Group(lobbyId).SendAsync("ReceiveMessage", message);
    }
}
