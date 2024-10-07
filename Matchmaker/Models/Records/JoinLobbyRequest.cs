using GameDomain.Models;

namespace Matchmaker.Models.Records;

public record JoinLobbyRequest(Player Player, string? Password, string Ip = "87.228.27.97");