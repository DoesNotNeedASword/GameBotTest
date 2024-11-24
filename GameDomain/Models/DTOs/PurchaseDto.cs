namespace GameDomain.Models.DTOs;

public class PurchaseDto
{
    public long BuyerId { get; set; }  // ID покупателя
    public Guid ListingId { get; set; }  // ID объявления
}
