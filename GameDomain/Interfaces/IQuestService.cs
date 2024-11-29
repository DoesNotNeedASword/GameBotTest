using GameDomain.Models;

namespace GameDomain.Interfaces
{
    public interface IQuestService
    {
        Task<List<Quest>> GetAvailableQuestsAsync();
        Task<bool> CheckAndRewardAsync(long playerId);
        Task<bool> CreateQuestAsync(Quest quest);
        Task<List<Quest>> GetAvailableQuestsForPlayerAsync(long playerId);
    }
}