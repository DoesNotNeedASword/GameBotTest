﻿using GameDomain.Interfaces;
using GameDomain.Models;
using MongoDB.Driver;

namespace GameAPI.Services;

public class PlayerService(IMongoDatabase database)
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

        try
        {
            await _players.InsertOneAsync(player);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
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
        var player = await (await _players.FindAsync(filter)).FirstOrDefaultAsync();

        if (player == null)
            return false;

        player.Rating += change;
        await _players.ReplaceOneAsync(filter, player);
        return true;
    }
    
    public async Task<List<Player>> GetTopPlayers(int count)
    {
        return await _players.Find(_ => true) 
            .SortByDescending(player => player.Rating) 
            .Limit(count)
            .ToListAsync();
    }

}
