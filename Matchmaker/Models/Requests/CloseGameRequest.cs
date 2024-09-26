namespace Matchmaker.Models.Requests;

public class CloseGameRequest
{
    public string RequestId { get; set; }
    public long Winner { get; set; }
    public List<long> Losers { get; set; }
}