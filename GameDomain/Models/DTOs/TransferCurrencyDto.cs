namespace GameDomain.Models.DTOs;

public class TransferCurrencyDto
{
    public long FromPlayerId { get; set; }
    public long ToPlayerId { get; set; }
    public long Amount { get; set; }
}
