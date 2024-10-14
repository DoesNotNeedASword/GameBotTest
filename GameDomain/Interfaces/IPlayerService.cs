using GameDomain.Models;
using GameDomain.Models.DTOs;

namespace GameDomain.Interfaces;

public interface IPlayerService
{
    /// <summary>
    /// Получить список всех игроков.
    /// </summary>
    Task<List<Player>> GetAsync();

    /// <summary>
    /// Получить игрока по TelegramId.
    /// </summary>
    Task<PlayerDto?> GetPlayerAsync(long id);

    /// <summary>
    /// Создать нового игрока.
    /// </summary>
    Task<Player> CreateAsync(Player player);

    /// <summary>
    /// Обновить данные игрока по его TelegramId.
    /// </summary>
    Task UpdateAsync(long id, Player playerIn);

    /// <summary>
    /// Удалить игрока по его TelegramId.
    /// </summary>
    Task RemoveAsync(long id);

    /// <summary>
    /// Обновить рейтинг игрока.
    /// </summary>
    Task<bool> UpdateRatingAsync(long telegramId, int change);

    /// <summary>
    /// Получить топ игроков по количеству очков.
    /// </summary>
    Task<List<Player>> GetTopPlayers(int count);

    /// <summary>
    /// Получить список рефералов пользователя.
    /// </summary>
    Task<List<Player>> GetReferralsAsync(long referrerId);

    /// <summary>
    /// Получить пригласителя для данного игрока.
    /// </summary>
    Task<Player?> GetReferrerAsync(long playerId);

    Task<string?> GetPlayerRegionIpAsync(long playerId);

    Task<bool> AssignRegionToPlayerAsync(long playerId, int regionId);
    Task<PlayerLobbyDto?> GetPlayerWithRegionIpAsync(long playerId);
}