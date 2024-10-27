using GameDomain.Models;

namespace Matchmaker.Interfaces;

public interface ILobbyCacheService
{
    Task<Lobby?> GetLobbyAsync(long lobbyId);
    Task SaveLobbyAsync(Lobby lobby);
    Task DeleteLobbyAsync(long lobbyId);
    Task UpdateLobbyPlayerCountAsync(long lobbyId, int playerCount);
    Task DeleteLobbyPlayerCountAsync(long lobbyId);
    Task<Lobby?> FindLobbyWithSinglePlayerAsync();
    Task<long> GenerateNewLobbyIdAsync();
    Task SetPlayerLobbyAsync(string playerLobbyKey, long lobbyId);
    Task RemovePlayerLobbyAsync(string playerLobbyKey);
    Task<long?> GetPlayerLobbyAsync(string playerLobbyKey);
    Task<Lobby?> GetLobbyByCreatorIdAsync(long id);
    Task<List<Lobby>> GetAllLobbiesAsync(string? filter = null);
}
