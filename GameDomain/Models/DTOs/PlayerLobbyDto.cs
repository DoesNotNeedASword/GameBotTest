namespace GameDomain.Models.DTOs;

public class PlayerLobbyDto
{
    public long TelegramId { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public int Score { get; set; }
    public long ReferrerId { get; set; }
    public long RegionId { get; set; }
    public string? RegionIp { get; set; }
}