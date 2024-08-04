using GameDomain.Interfaces;
using GameDomain.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameAPI.Services;

public class PlayerService(IMongoDatabase database, ILevelService levelService)
{
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    public async Task<List<Player>> GetAsync()
    {
        var cursor = await _players.FindAsync(player => true);
        var list = await cursor.ToListAsync();
        return list;
    }

    public async Task<Player> GetAsync(long id)
    {
        var cursor = await _players.FindAsync(player => player.TelegramId == id);
        return await cursor.FirstOrDefaultAsync();
    }

    public async Task<Player> CreateAsync(Player player)
    {
        var existingPlayer = await _players.Find(p => p.TelegramId == player.TelegramId).FirstOrDefaultAsync();
        if (existingPlayer != null)
        {
            throw new Exception("A player with the same TelegramId already exists.");
        }

        if (!string.IsNullOrEmpty(player.ReferrerId))
        {
            var referrerExists = await _players.Find(p => p.Id == player.ReferrerId).AnyAsync();
            if (!referrerExists)
            {
                throw new Exception("Referral ID does not correspond to any existing player.");
            }
        }

        if (!ObjectId.TryParse(player.Id, out _))
        {
            player.Id = ObjectId.GenerateNewId().ToString();
        }

        await _players.InsertOneAsync(player);
        return player;
    }

    public async Task UpdateAsync(long id, Player playerIn)
    {
        await _players.ReplaceOneAsync(player => player.TelegramId == id, playerIn);
    }

    public async Task RemoveAsync(long id)
    {
        await _players.DeleteOneAsync(player => player.TelegramId == id);
    }
    public async Task<bool> UpdateRatingAsync(string id, int change)
    {
        var player = await _players.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (player == null)
            throw new Exception("NotFound");

        player.Rating += change;
        var newLevel = levelService.CheckLevel(player.Rating);

        if (newLevel != player.Level)
            player.Level = newLevel;

        var update = Builders<Player>.Update
            .Set(p => p.Rating, player.Rating)
            .Set(p => p.Level, player.Level);
        
        var result = await _players.UpdateOneAsync(p => p.Id == id, update);
        return result.ModifiedCount == 1;
    }
    
    public async Task<List<Player>> GetTopPlayers(int count)
    {
        return await _players.Find(_ => true) 
            .SortByDescending(player => player.Rating) 
            .Limit(count)
            .ToListAsync();
    }

}
