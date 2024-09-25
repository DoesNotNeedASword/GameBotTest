using GameDomain.Models;

namespace Matchmaker.Interfaces;

public interface IEdgegapService
{
    Task<string?> StartEdgegapServer(Lobby lobby);
    Task<(string? Dns, int? ExternalPort)> GetEdgegapServerStatus(string requestId);
}