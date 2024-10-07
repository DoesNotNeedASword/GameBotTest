using GameDomain.Models;

namespace Matchmaker.Interfaces;

public interface IEdgegapService
{
    Task<string?> StartEdgegapServer(Lobby lobby, List<string> ipList);
    Task<(string? Dns, int? ExternalPort)> GetEdgegapServerStatus(string requestId);
    Task<bool> StopDeployment(string requestId);
}