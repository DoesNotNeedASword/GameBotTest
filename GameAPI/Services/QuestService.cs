using GameDomain.Interfaces;
using GameDomain.Models;
using MongoDB.Driver;

namespace GameAPI.Services;

public class QuestService(IMongoDatabase database, IPlayerService playerService) : IQuestService
{
    private readonly IMongoCollection<Quest> _quests = database.GetCollection<Quest>("Quests");
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    public async Task<List<Quest>> GetAvailableQuestsAsync()
    {
        return await _quests.Find(_ => true).ToListAsync();
    }

    public async Task<bool> CheckAndRewardAsync(long playerId)
    {
        var player = await playerService.GetFullPlayerAsync(playerId);
        if (player == null) return false;

        var quests = await GetAvailableQuestsAsync();
        var newlyCompletedQuests = new List<Quest>();

        foreach (var quest in quests.Where(quest => !player.CompletedQuests.Contains(quest.Id)))
        {
            if (!player.Statistics.TryGetValue(quest.RequirementKey, out var statValue) ||
                statValue.AsInt32 < quest.RequirementValue) continue;

            player.SoftCurrency += quest.Reward;
            newlyCompletedQuests.Add(quest);
        }

        // Добавляем выполненные задания в список игрока
        foreach (var completedQuest in newlyCompletedQuests)
        {
            player.CompletedQuests.Add(completedQuest.Id);
        }
        
        await playerService.UpdateAsync(playerId, player);
        return true;
    }
    
    public async Task<bool> CreateQuestAsync(Quest quest)
    {
        if (string.IsNullOrEmpty(quest.Name) || quest.Reward <= 0)
        {
            return false;
        }

        var existingQuest = await _quests.Find(q => q.Name == quest.Name).FirstOrDefaultAsync();
        if (existingQuest != null)
        {
            return false; 
        }

        await _quests.InsertOneAsync(quest);
        return true;
    }

    public async Task<List<Quest>> GetAvailableQuestsForPlayerAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player == null) return [];

        var completedQuestIds = player.CompletedQuests;

        var availableQuests = await _quests.Find(q => !completedQuestIds.Contains(q.Id)).ToListAsync();
        return availableQuests;
    }

}

