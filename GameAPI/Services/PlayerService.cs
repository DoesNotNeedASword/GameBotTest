using GameAPI.Models;
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

    public async Task<Player> GetAsync(string id)
    {
        var cursor = await _players.FindAsync(player => player.Id == id);
        return await cursor.FirstOrDefaultAsync();
    }

    public async Task<Player> CreateAsync(Player player)
    {
        if (!string.IsNullOrEmpty(player.ReferrerId))
        {
            var referrerExists = await _players.Find(p => p.Id == player.ReferrerId).AnyAsync();
            if (!referrerExists)
            {
                throw new Exception("Referral ID does not correspond to any existing player.");
            }
        }

        if (!ObjectId.TryParse(player.Id, out _))
            player.Id = ObjectId.GenerateNewId().ToString();
    
        await _players.InsertOneAsync(player);
        return player;
    }

    public async Task UpdateAsync(string id, Player playerIn)
    {
        await _players.ReplaceOneAsync(player => player.Id == id, playerIn);
    }

    public async Task RemoveAsync(string id)
    {
        await _players.DeleteOneAsync(player => player.Id == id);
    }
}
