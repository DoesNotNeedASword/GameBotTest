﻿using GameDomain.Interfaces;
using GameDomain.Models;
using GameDomain.Models.DTOs;
using MongoDB.Driver;

namespace GameAPI.Services;

public class CarService(IMongoDatabase database) : ICarService
{
    private readonly IMongoCollection<Car> _cars = database.GetCollection<Car>("Cars");
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    public async Task AddCarToPlayerAsync(long playerId, Car car)
    {
        car.PlayerId = playerId;
        await _cars.InsertOneAsync(car);
    }

    public async Task<List<Car>> GetCarsByPlayerAsync(long playerId)
    {
        return await _cars.Find(car => car.PlayerId == playerId).ToListAsync();
    }

    public async Task<Car?> GetCarByIdAsync(string carId)
    {
        return await _cars.Find(car => car.Id == carId).FirstOrDefaultAsync();
    }

    public async Task<bool> CustomizeCarAsync(CarCustomizationDto dto)
    {
        var update = Builders<Car>.Update
            .Set(car => car.WheelId, dto.WheelId)
            .Set(car => car.SpoilerId, dto.SpoilerId)
            .Set(car => car.ColorId, dto.ColorId);

        var result = await _cars.UpdateOneAsync(car => car.Id == dto.CarId, update);
        return result.ModifiedCount > 0;
    }
    public async Task<bool> RemoveCarAsync(string carId)
    {
        var result = await _cars.DeleteOneAsync(car => car.Id == carId);
        return result.DeletedCount > 0;
    }

    public async Task<bool> TransferCarOwnershipAsync(string carId, long newOwnerId)
    {
        var car = await _cars.Find(c => c.Id == carId).FirstOrDefaultAsync();
        if (car == null) return false;

        var newOwner = await _players.Find(p => p.TelegramId == newOwnerId).FirstOrDefaultAsync();
        if (newOwner == null) return false;

        car.PlayerId = newOwnerId;
        await _cars.ReplaceOneAsync(c => c.Id == carId, car);
        return true;
    }
}