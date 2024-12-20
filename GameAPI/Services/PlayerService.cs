﻿using GameDomain.Interfaces;
using GameDomain.Models;
using GameDomain.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameAPI.Services;

public class PlayerService(IMongoDatabase database, IEnergyService energyService) : IPlayerService
{
    private readonly IMongoCollection<Player> _players = database.GetCollection<Player>("Players");
    private readonly IMongoCollection<Region> _regions = database.GetCollection<Region>("Regions");

    public async Task<List<Player>> GetAsync()
    {
        var cursor = await _players.FindAsync(player => true);
        var list = await cursor.ToListAsync();
        return list;
    }

    public async Task<PlayerDto?> GetPlayerAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        return player is null ? null : new PlayerDto(player);
    }
    public async Task<Player?> GetFullPlayerAsync(long playerId) => await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();

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
        await energyService.CreateAsync(player.TelegramId);

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
    public async Task<bool> AssignRegionToPlayerAsync(long playerId, int regionId)
    {
        var regionExists = await _regions.Find(r => r.RegionId == regionId).AnyAsync();
        if (!regionExists)
        {
            throw new Exception("Region does not exist.");
        }

        var filter = Builders<Player>.Filter.Eq(p => p.TelegramId, playerId);
        var update = Builders<Player>.Update.Set(p => p.RegionId, regionId);
        var result = await _players.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    public async Task<string?> GetPlayerRegionIpAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).Project(p => new { p.RegionId }).FirstOrDefaultAsync();
        if (player == null) return null;

        var region = await _regions.Find(r => r.RegionId == player.RegionId).Project(r => r.Ip).FirstOrDefaultAsync();
        return region;
    }
    
    
    public async Task<PlayerLobbyDto?> GetPlayerWithRegionIpAsync(long playerId)
    {
        var player = await GetPlayerAsync(playerId);
        if (player == null) return null;

        var regionIp = await GetPlayerRegionIpAsync(playerId);
        return new PlayerLobbyDto
        {
            TelegramId = player.TelegramId,
            Name = player.Name,
            Level = player.Level,
            Score = player.Score,
            ReferrerId = player.ReferrerId,
            RegionId = player.RegionId,
            RegionIp = regionIp
        };
    }
    public async Task<bool> UpdatePlayerCustomizationAsync(long playerId, PlayerCustomizationDto customizationDto)
    {
        var updateDefinitionBuilder = Builders<Player>.Update;
        var updates = new List<UpdateDefinition<Player>>();

        if (customizationDto.AvatarId != 0)
        {
            updates.Add(updateDefinitionBuilder.Set(p => p.AvatarId, customizationDto.AvatarId));
        }

        if (customizationDto.FrameId != 0)
        {
            updates.Add(updateDefinitionBuilder.Set(p => p.FrameId, customizationDto.FrameId));
        }

        if (customizationDto.TitleId != 0)
        {
            updates.Add(updateDefinitionBuilder.Set(p => p.TitleId, customizationDto.TitleId));
        }

        if (customizationDto.PhraseId != 0)
        {
            updates.Add(updateDefinitionBuilder.Set(p => p.PhraseId, customizationDto.PhraseId));
        }

        if (updates.Count == 0) return false;  

        var update = updateDefinitionBuilder.Combine(updates);
        var result = await _players.UpdateOneAsync(p => p.TelegramId == playerId, update);

        return result.MatchedCount > 0;
    }
    public async Task<bool> TransferCurrencyAsync(long fromPlayerId, long toPlayerId, long amount)
    {
        var fromPlayer = await _players.Find(p => p.TelegramId == fromPlayerId).FirstOrDefaultAsync();
        var toPlayer = await _players.Find(p => p.TelegramId == toPlayerId).FirstOrDefaultAsync();

        if (fromPlayer == null || toPlayer == null || fromPlayer.SoftCurrency < amount)
        {
            return false;
        }

        fromPlayer.SoftCurrency -= amount;
        toPlayer.SoftCurrency += amount;

        await _players.ReplaceOneAsync(p => p.TelegramId == fromPlayerId, fromPlayer);
        await _players.ReplaceOneAsync(p => p.TelegramId == toPlayerId, toPlayer);

        return true;
    }
    
    public async Task<bool> UpdatePlayerStatisticsAsync(long playerId, Dictionary<string, int> updates)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player == null) return false;


        player.Statistics.AddRange(updates);
        

        var result = await _players.ReplaceOneAsync(p => p.TelegramId == playerId, player);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }
    
    public async Task<bool> UpdateDailyLoginAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player == null) return false;

        var currentDate = DateTime.UtcNow.Date;

        if (player.LastLoginDate.Date == currentDate) return true;

        if (player.LastLoginDate.Date == currentDate.AddDays(-1))
        {
            player.LoginStreak++;
        }
        else
        {
            player.LoginStreak = 1;
            player.MissedDayCompensation = true; 
        }
        if (player.LoginStreak > player.MaxLoginStreak)
        {
            player.MaxLoginStreak = player.LoginStreak;
        }
        player.LastLoginDate = currentDate;
        var result = await _players.ReplaceOneAsync(p => p.TelegramId == playerId, player);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> CompensateMissedDayAsync(long playerId)
    {
        var player = await _players.Find(p => p.TelegramId == playerId).FirstOrDefaultAsync();
        if (player == null) return false;

        if (!player.MissedDayCompensation) return false;

        player.LoginStreak++;
        player.MissedDayCompensation = false; 

        if (player.LoginStreak > player.MaxLoginStreak)
        {
            player.MaxLoginStreak = player.LoginStreak;
        }

        var result = await _players.ReplaceOneAsync(p => p.TelegramId == playerId, player);
        return result.ModifiedCount > 0;
    }


}





