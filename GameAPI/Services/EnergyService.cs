using GameDomain.Interfaces;
using GameDomain.Models;
using MongoDB.Driver;
using System.Collections.Concurrent;
using GameAPI.Models;
using GameDomain.Models.DTOs;

namespace GameAPI.Services;

public class EnergyService(IMongoDatabase database) : IEnergyService
{
    private readonly IMongoCollection<EnergyStation> _energyStations = database.GetCollection<EnergyStation>("EnergyStations");
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    private static readonly ConcurrentDictionary<int, EnergyStationLevelData> LevelData = new()
    {
        [0] = new EnergyStationLevelData { Level = 0, MaxEnergy = 5, UpgradeCost = 5000, RefillIntervalMinutes = 120, MinDaysBeforeNextUpgrade = 0 },
        [1] = new EnergyStationLevelData { Level = 1, MaxEnergy = 6, UpgradeCost = 10000, RefillIntervalMinutes = 120, MinDaysBeforeNextUpgrade = 8 },
        [2] = new EnergyStationLevelData { Level = 2, MaxEnergy = 7, UpgradeCost = 25000, RefillIntervalMinutes = 120, MinDaysBeforeNextUpgrade = 20 },
        [3] = new EnergyStationLevelData { Level = 3, MaxEnergy = 8, UpgradeCost = 50000, RefillIntervalMinutes = 120, MinDaysBeforeNextUpgrade = 40 },
        [4] = new EnergyStationLevelData { Level = 4, MaxEnergy = 9, UpgradeCost = 100000, RefillIntervalMinutes = 120, MinDaysBeforeNextUpgrade = 80 },
        [5] = new EnergyStationLevelData { Level = 5, MaxEnergy = 10, UpgradeCost = 200000, RefillIntervalMinutes = 120, MinDaysBeforeNextUpgrade = 0 }
    };

    public async Task<bool> CreateAsync(long playerId)
    {
        var existingStation = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (existingStation is not null)
        {
            return false; // Станция уже существует
        }

        var levelData = LevelData[0];
        var newStation = new EnergyStation
        {
            PlayerId = playerId,
            MaxEnergy = levelData.MaxEnergy,
            RefillIntervalMinutes = levelData.RefillIntervalMinutes,
            UpgradeCost = levelData.UpgradeCost,
            NextUpgradeAvailableDate = DateTime.UtcNow
        };

        await _energyStations.InsertOneAsync(newStation);
        return true;
    }

    public async Task<PlayerDto?> ConsumeEnergy(long playerId, int value)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player is null || player.CurrentEnergy < value)
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
        var station = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        return station?.MaxEnergy ?? LevelData[0].MaxEnergy;
    }

    public async Task<bool> RefillEnergyAsync(long playerId)
    {
        var station = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (station is null) return false;

        var elapsed = DateTime.UtcNow - station.LastRefillTime;
        var energyToRefill = (int)(elapsed.TotalMinutes / station.RefillIntervalMinutes);

        if (energyToRefill <= 0) return false;

        station.CurrentEnergy = Math.Min(station.CurrentEnergy + energyToRefill, station.MaxEnergy);
        station.LastRefillTime = DateTime.UtcNow;

        await _energyStations.ReplaceOneAsync(s => s.PlayerId == playerId, station);
        return true;
    }

    public async Task<bool> StartUpgradeAsync(long playerId)
    {
        var station = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (station is null || station.UpgradeStartTime.HasValue) return false;

        if (!LevelData.TryGetValue(station.Level + 1, out var nextLevelData)) return false;

        if (DateTime.UtcNow < station.NextUpgradeAvailableDate) return false;

        station.UpgradeStartTime = DateTime.UtcNow;
        station.UpgradeDurationMinutes = 240;

        await _energyStations.ReplaceOneAsync(s => s.PlayerId == playerId, station);
        return true;
    }

    public async Task<bool> ReduceUpgradeTimeAsync(long playerId)
    {
        var station = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (station?.UpgradeStartTime == null) return false;

        var elapsed = DateTime.UtcNow - station.UpgradeStartTime.Value;
        if (elapsed.TotalMinutes >= 120) return false;

        station.UpgradeDurationMinutes = 120;

        await _energyStations.ReplaceOneAsync(s => s.PlayerId == playerId, station);
        return true;
    }

    public async Task<bool> CompleteUpgradeAsync(long playerId)
    {
        var station = await _energyStations.Find(s => s.PlayerId == playerId).FirstOrDefaultAsync();
        if (station?.UpgradeStartTime == null) return false;

        var elapsed = DateTime.UtcNow - station.UpgradeStartTime.Value;
        if (elapsed.TotalMinutes < station.UpgradeDurationMinutes) return false;

        if (!LevelData.TryGetValue(station.Level + 1, out var nextLevelData)) return false;

        station.Level++;
        station.MaxEnergy = nextLevelData.MaxEnergy;
        station.RefillIntervalMinutes = nextLevelData.RefillIntervalMinutes;
        station.UpgradeCost = nextLevelData.UpgradeCost;
        station.NextUpgradeAvailableDate = DateTime.UtcNow.AddDays(nextLevelData.MinDaysBeforeNextUpgrade);
        station.UpgradeStartTime = null;

        await _energyStations.ReplaceOneAsync(s => s.PlayerId == playerId, station);
        return true;
    }
}
