namespace GameDomain.Models.DTOs;

public class ListingDto
{
    public Guid Id { get; set; } = Guid.NewGuid(); 
    public long SellerId { get; set; } 
    public string CarId { get; set; } 
    public int Price { get; set; } 
    public DateTime ListedAt { get; set; } = DateTime.UtcNow;
}
