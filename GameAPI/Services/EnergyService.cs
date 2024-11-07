using GameDomain.Interfaces;
using GameDomain.Models;
using GameDomain.Models.DTOs;
using MongoDB.Driver;

namespace GameAPI.Services;

public class EnergyService(IMongoDatabase database) : IEnergyService
{
    private readonly IMongoCollection<EnergyStation> _energyStations = database.GetCollection<EnergyStation>("EnergyStations");
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    public async Task<bool> CreateAsync(long playerId)
    {
        var existingStation = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (existingStation is not null)
        {
            return false; // Station already exists, no need to create another
        }

        var newStation = new EnergyStation
        {
            PlayerId = playerId,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10, // Default refill rate
            UpgradeCost = 100 // Default upgrade cost
        };

        await _energyStations.InsertOneAsync(newStation);
        return true;
    }

    public async Task<PlayerDto?> ConsumeEnergy(long playerId, int value)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player.CurrentEnergy < value)
            return null;
        player.CurrentEnergy -= value;
        await _players.ReplaceOneAsync(p => p.TelegramId == playerId, player);
        return new PlayerDto(player);
    }
    
    public async Task<int> GetEnergyAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        return player?.CurrentEnergy ?? 0;
    }

    public async Task<int> GetMaxEnergyAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        return player?.MaxEnergy ?? 100; // Default max energy
    }

    public async Task<bool> RefillEnergyAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        var energyStation = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (player is null || player.CurrentEnergy >= player.MaxEnergy)
        {
            return false;
        }

        player.CurrentEnergy = player.MaxEnergy;
        energyStation.LastRefillTime = DateTime.UtcNow;
        await _players.ReplaceOneAsync(p => p.TelegramId == playerId, player);
        return true;
    }
    
    public async Task<bool> UpgradeRefillStationAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        var station = await _energyStations.Find(e => e.PlayerId == playerId).FirstOrDefaultAsync();
        
        if (station is null)
            return false;
        if (player is null || player.SoftCurrency < station.UpgradeCost) 
            return false;
        player.SoftCurrency -= station.UpgradeCost;
        
        station.Level++;
        station.RefillRate += 10;
        station.UpgradeCost += 50;

        await _energyStations.ReplaceOneAsync(e => e.PlayerId == playerId, station);
        await _players.ReplaceOneAsync(p => p.TelegramId == playerId, player);

        return true;
    }

}