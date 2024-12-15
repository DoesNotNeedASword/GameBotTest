using GameDomain.Models;
using System.Collections.Concurrent;
using GameDomain.Models.DTOs;

namespace GameDomain.Interfaces;

public interface IEnergyService
{
    Task<bool> CreateAsync(long playerId);
    Task<PlayerDto?> ConsumeEnergy(long playerId, int value);
    Task<int> GetEnergyAsync(long playerId);
    Task<int> GetMaxEnergyAsync(long playerId);
    Task<bool> RefillEnergyAsync(long playerId);
    Task<bool> StartUpgradeAsync(long playerId);
    Task<bool> ReduceUpgradeTimeAsync(long playerId);
    Task<bool> CompleteUpgradeAsync(long playerId);
}