using GameDomain.Models;
using GameDomain.Models.DTOs;

namespace GameDomain.Interfaces;

public interface ICarService
{
    Task AddCarToPlayerAsync(long playerId, Car car);
    Task<List<Car>> GetCarsByPlayerAsync(long playerId);
    Task<Car?> GetCarByIdAsync(string carId);
    Task<bool> CustomizeCarAsync(CarCustomizationDto customizationDto);
    Task<bool> RemoveCarAsync(string carId);
    Task<bool> TransferCarOwnershipAsync(string carId, long newOwnerId);
}