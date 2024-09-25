using GameDomain.Models;

namespace Matchmaker.Models.Records;

public record LeaveLobbyRequest(long LobbyId, long PlayerId);