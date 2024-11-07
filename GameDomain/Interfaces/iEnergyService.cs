using System.Threading.Tasks;
using GameDomain.Models.DTOs;

namespace GameDomain.Interfaces
{
    public interface IEnergyService
    {
        Task<int> GetEnergyAsync(long playerId); 
        Task<int> GetMaxEnergyAsync(long playerId); 
        Task<bool> RefillEnergyAsync(long playerId); 
        Task<bool> UpgradeRefillStationAsync(long playerId);
        Task<bool> CreateAsync(long playerId);
        Task<PlayerDto?> ConsumeEnergy(long playerId, int value);
    }
}