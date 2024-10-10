using GameDomain.Interfaces;
using GameDomain.Models;
using MongoDB.Driver;

namespace GameAPI.Services;

public class PlayerService(IMongoDatabase database) : IPlayerService
{
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    public async Task<List<Player>> GetAsync()
    {
        var cursor = await _players.FindAsync(player => true);
        var list = await cursor.ToListAsync();
        return list;
    }

    public async Task<Player?> GetAsync(long id)
    {
        var cursor = await _players.FindAsync(player => player.TelegramId == id);
        return await cursor.FirstOrDefaultAsync();
    }

    public async Task<Player> CreateAsync(Player player)
    {
        var existingPlayer = await (await _players.FindAsync(p => p.TelegramId == player.TelegramId)).FirstOrDefaultAsync();
        if (existingPlayer != null)
        {
            throw new Exception("A player with the same TelegramId already exists.");
        }

        if (player.ReferrerId != 0)
        {
            var referrerExists = await (await _players.FindAsync(p => p.TelegramId == player.ReferrerId)).AnyAsync();
            if (!referrerExists)
            {
                throw new Exception("Referral ID does not correspond to any existing player.");
            }
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
    
    public async Task<bool> UpdateRatingAsync(long telegramId, int change)
    {
        var filter = Builders<Player>.Filter.Eq(p => p.TelegramId, telegramId);
        var update = Builders<Player>.Update.Inc(p => p.Score, change);
        var result = await _players.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    public async Task<List<Player>> GetTopPlayers(int count)
    {
        return await _players.Find(_ => true) 
            .SortByDescending(player => player.Score) 
            .Limit(count)
            .ToListAsync();
    }
    public async Task<List<Player>> GetReferralsAsync(long referrerId)
    {
        var filter = Builders<Player>.Filter.Eq(p => p.ReferrerId, referrerId);
        var sort = Builders<Player>.Sort.Descending(p => p.Score);
        return await _players.Find(filter).Sort(sort).ToListAsync();
    }

    public async Task<Player?> GetReferrerAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player == null) return null;

        var referrerFilter = Builders<Player>.Filter.Eq(p => p.TelegramId, player.ReferrerId);
        return await _players.Find(referrerFilter).FirstOrDefaultAsync();
    }
}

