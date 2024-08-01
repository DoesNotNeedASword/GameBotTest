using GameDomain.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameAPI.Services;

public class PlayerService(IMongoDatabase database)
{
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");

    public async Task<List<Player>> GetAsync()
    {
        var cursor = await _players.FindAsync(player => true);
        return await cursor.ToListAsync();
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
}
