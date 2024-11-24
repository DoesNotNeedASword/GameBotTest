namespace GameDomain.Models.DTOs;


public class TransferCarDto(string carId, long newOwnerId )
{
    public string CarId { get; set; } = carId;
    public long NewOwnerId { get; set; } = newOwnerId;
}
