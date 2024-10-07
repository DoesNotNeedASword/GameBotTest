using GameDomain.Models;

namespace Matchmaker.Models.Records;

public record JoinLobbyRequest(Player Player, string? Password);