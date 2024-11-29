using System.Text.Json;
using System.Web;
using GameAPI.Extensions;
using GameAPI.Models;
using GameAPI.Models.DTO;
using GameAPI.Services;
using GameAPI.Verification.Model;
using GameDomain.Interfaces;
using GameDomain.Models;
using GameDomain.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

var builder = WebApplication.CreateBuilder(args);

var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

const string cacheKey = "players";
const string referrerKey = "referrer";

await MongoDbSeederSevice.SeedAsync(builder.AddMongoDb()); 
builder.AddServices();
const string constantKey = "WebAppData";
var botToken = builder.Configuration["BOT_TOKEN"]!;


var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseStaticFiles();

app.MapGet("/api/players", async (IPlayerService playerService, ICacheService cacheService) =>
{
    var cachedPlayers = await cacheService.GetAsync(cacheKey);
    if (!string.IsNullOrEmpty(cachedPlayers))
    {
        return Results.Ok(JsonSerializer.Deserialize<List<Player>>(cachedPlayers));
    }

    var players = await playerService.GetAsync();
    await cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(players));
    return Results.Ok(players);
}).RequireAuthorization();

app.MapGet("/api/players/{id:long}", async (long id, ICacheService cacheService) =>
{
    var player = await cacheService.GetPlayerAsync(id);
    return player != null ? Results.Ok(player) : Results.NotFound();
}).WithName("GetPlayer").AllowAnonymous();

app.MapPost("/api/players", async ([FromBody] Player player, IPlayerService playerService, ICacheService cacheService) =>
{
    await playerService.CreateAsync(player);
    await cacheService.SetAsync($"{cacheKey}:{player.TelegramId}", JsonSerializer.Serialize(player));
    return Results.Ok(player);
}).AllowAnonymous();

app.MapPut("/api/players/{id:long}", async (long id, Player playerIn, IPlayerService playerService,
    ICacheService cacheService) =>
{
    var player = await cacheService.GetPlayerAsync(id);
    if(player is null)
        return Results.NotFound();
    await playerService.UpdateAsync(id, playerIn);
    return Results.Ok();
}).AllowAnonymous();

app.MapPut("/api/players/{telegramId:long}/rating", async (long telegramId, [FromBody] int ratingChange,
    IPlayerService playerService, ICacheService cacheService) =>
{
    var success = await playerService.UpdateRatingAsync(telegramId, ratingChange);

    if (!success) return Results.NotFound($"Player with Telegram ID {telegramId} not found.");
    await cacheService.RemoveAsync(cacheKey);  
    return Results.Ok();

}).AllowAnonymous();

app.MapDelete("/api/players/{id:long}", async (long id, IPlayerService playerService,
    ICacheService cacheService) =>
{
    await playerService.RemoveAsync(id);
    await cacheService.RemoveAsync($"{cacheKey}:{id}");  
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/leaders", async ([FromServices] ICacheService cacheService) =>
{
    try
    {
        var leaders = await cacheService.GetTopPlayersAsync();
        return leaders.Count == 0 ? Results.NotFound("No leaders found.") : Results.Ok(leaders);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).AllowAnonymous();


app.MapPost("/api/verify", async (HttpRequest request) =>
{
    var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
    var payload = JsonSerializer.Deserialize<TgPayloadDto>(requestBody, jsonSerializerOptions);

    if (payload == null || string.IsNullOrEmpty(payload.InitData))
    {
        return Results.BadRequest();
    }
    var data = HttpUtility.ParseQueryString(payload.InitData);

    return VerificationService.IsValidData(data, constantKey, botToken) ? Results.Ok(new { valid = true }) : Results.BadRequest();
}).AllowAnonymous();


app.MapPost("/api/login", async (LoginModel login, IConfiguration config, JwtService jwtService) =>
{
    if (login.Username != config["SERVICE_USERNAME"] || login.Password != config["SERVICE_PASSWORD"])
        return Results.Unauthorized();
    var token = jwtService.GenerateToken(login.Username);
    return Results.Ok(new { token });
}).AllowAnonymous();

app.MapGet("/api/players/referrals/{telegramId:long}", async (long telegramId, IPlayerService playerService) =>
{
    var referrals = await playerService.GetReferralsAsync(telegramId);
    return Results.Ok(referrals);
}).AllowAnonymous();

app.MapGet("/api/players/referrer/{telegramId:long}", async (long telegramId, IPlayerService playerService, ICacheService cacheService) =>
{
    var cachedReferrer = await cacheService.GetAsync($"{referrerKey}:{telegramId}");
    if (!string.IsNullOrEmpty(cachedReferrer))
    {
        return Results.Ok(JsonSerializer.Deserialize<Player>(cachedReferrer));
    }

    var referrer = await playerService.GetReferrerAsync(telegramId);
    if (referrer == null) return Results.NotFound("Referrer not found.");
    await cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(referrer));
    return Results.Ok(referrer);

}).AllowAnonymous();
app.MapGet("/api/players/ip/{playerId:long}", async (long playerId, IPlayerService playerService) =>
{
    var regionIp = await playerService.GetPlayerRegionIpAsync(playerId);
    
    return string.IsNullOrEmpty(regionIp) ? Results.NotFound()
        : Results.Ok(new { RegionIp = regionIp });
}).WithName("GetPlayerRegionIp").AllowAnonymous();

app.MapPost("/api/players/region", async ([FromBody] SetRegionDto assignRegionDto, IPlayerService playerService, ICacheService cacheService) =>
{
    var success = await playerService.AssignRegionToPlayerAsync(assignRegionDto.PlayerId, assignRegionDto.RegionId);

    if (!success) return Results.BadRequest();
    var player = await playerService.GetPlayerAsync(assignRegionDto.PlayerId);
        
    if (player != null)
    {
        await cacheService.SetAsync($"player:{player.TelegramId}", JsonSerializer.Serialize(player));
    }

    return Results.Ok();

}).WithName("SetRegionToPlayer").AllowAnonymous();

app.MapPost("/api/player/energy/refill/{playerId:long}", async (long playerId, IEnergyService energyService) =>
{
    var success = await energyService.RefillEnergyAsync(playerId);
    return success ? Results.Ok("Energy refilled") : Results.BadRequest("Cannot refill energy yet.");
}).WithName("RefillEnergy");

app.MapPost("/api/player/energy/upgrade/{playerId:long}", async (long playerId, IEnergyService energyService) =>
{
    var success = await energyService.UpgradeRefillStationAsync(playerId);
    return success ? Results.Ok("Energy station upgraded") : Results.BadRequest("Upgrade failed.");
}).WithName("UpgradeEnergyStation");

app.MapPut("/api/player/energy/consume", async ([FromBody] ConsumeEnergyDto dto, [FromServices] IEnergyService energyService) =>
{
    var player = await energyService.ConsumeEnergy(dto.PlayerId, dto.Value);
    return player is null ? Results.Ok(player) : Results.BadRequest("Upgrade failed.");
}).WithName("ConsumeEnergy");

app.MapPut("/api/player/customization/{telegramId:long}", async (long telegramId,
    [FromBody] PlayerCustomizationDto customizationDto, IPlayerService service) =>
{
    var result = await service.UpdatePlayerCustomizationAsync(telegramId, customizationDto);

    return result ? Results.Ok("Customization updated successfully") : Results.NotFound("Error");
}).WithName("UpdateCustomization");


app.MapPost("/api/cars/{playerId:long}", async (long playerId, Car car, ICarService carsService) =>
{
    await carsService.AddCarToPlayerAsync(playerId, car);
    return Results.Ok(car);
}).WithTags("Cars");

app.MapGet("/api/cars/{playerId:long}", async (long playerId, ICarService carsService) =>
{
    var cars = await carsService.GetCarsByPlayerAsync(playerId);
    return Results.Ok(cars);
}).WithTags("Cars");

app.MapGet("/api/cars/{carId}", async (string carId, ICarService carsService) =>
{
    var car = await carsService.GetCarByIdAsync(carId);
    return car != null ? Results.Ok(car) : Results.NotFound("Car not found");
}).WithTags("Cars");

app.MapPut("/api/cars/customize", async (CarCustomizationDto dto, ICarService carsService) =>
{
    var success = await carsService.CustomizeCarAsync(dto);
    return success ? Results.Ok("Car customized") : Results.NotFound("Car not found");
}).WithTags("Cars");

app.MapDelete("/api/cars/{carId}", async (string carId, ICarService carsService) =>
{
    var success = await carsService.RemoveCarAsync(carId);
    return success ? Results.Ok("Car removed") : Results.NotFound("Car not found");
}).WithTags("Cars");

app.MapPut("/api/cars/transfer", async (TransferCarDto transferDto, ICarService carsService) =>
{
    var success = await carsService.TransferCarOwnershipAsync(transferDto.CarId, transferDto.NewOwnerId);
    return success ? Results.Ok("Car ownership transferred") : Results.NotFound("Car or owner not found");
}).WithName("TransferCarOwnership").WithTags("Cars");


app.MapGet("/api/players/data/{telegramId:long}", async (long telegramId, IPlayerService playerService) =>
{
    var player = await playerService.GetPlayerAsync(telegramId);
    return player != null ? Results.Ok(player) : Results.NotFound("Player not found");
}).WithName("GetPlayerData");

app.MapPut("/api/players/update/{telegramId:long}", async (long telegramId, Player player, IPlayerService playerService) =>
{
    await playerService.UpdateAsync(telegramId, player);
    return Results.Ok("Player updated");
}).WithName("UpdatePlayerData");

app.MapPost("/api/players/transfer", async ([FromBody] TransferCurrencyDto transferDto, IPlayerService playerService) =>
{
    var success = await playerService.TransferCurrencyAsync(transferDto.FromPlayerId, transferDto.ToPlayerId, transferDto.Amount);
    return success ? Results.Ok("Transaction completed") : Results.BadRequest("Transaction failed");
}).WithName("TransferCurrency");

app.MapGet("/api/players/stat/{id:long}", async (long id, IPlayerService playerService) =>
{
    var player = await playerService.GetFullPlayerAsync(id);
    return player == null ? Results.NotFound($"Player with ID {id} not found.") : Results.Ok(player.Statistics.ToDictionary());
}).WithName("GetPlayerStatistics").WithTags("Statistics");

app.MapGet("/api/players/stat/{id:long}/{key}", async (long id, string key, IPlayerService playerService) =>
{
    var player = await playerService.GetFullPlayerAsync(id);
    if (player == null) return Results.NotFound($"Player with ID {id} not found.");

    return !player.Statistics.Contains(key) ? 
        Results.NotFound($"Statistic '{key}' not found for player with ID {id}.") : 
        Results.Ok(new { Key = key, Value = player.Statistics[key] });
}).WithName("GetPlayerStatisticByKey").WithTags("Statistics");

app.MapPut("/api/players/stat/{id:long}", async (long id,
    [FromBody] Dictionary<string, int> updates, IPlayerService playerService) =>
{
    var success = await playerService.UpdatePlayerStatisticsAsync(id, updates);
    return success ? Results.Ok($"Statistics for player {id} updated.") : Results.NotFound($"Player with ID {id} not found.");
}).WithName("UpdatePlayerStatistics").WithTags("Statistics");

app.MapGet("/api/quests", async (IQuestService questService) =>
{
    var quests = await questService.GetAvailableQuestsAsync();
    return Results.Ok(quests);
}).WithName("GetAvailableQuests").WithTags("Quests");

app.MapPost("/api/quests/check/{playerId:long}", async (long playerId, IQuestService questService) =>
{
    var success = await questService.CheckAndRewardAsync(playerId);
    return success 
        ? Results.Ok("Quests checked and rewards applied if any.") 
        : Results.NotFound("Player not found or no quests completed.");
}).WithName("CheckAndRewardQuests").WithTags("Quests");

app.MapPost("/api/quests/create", async (Quest quest, IQuestService questService) =>
{
    var success = await questService.CreateQuestAsync(quest);
    return success 
        ? Results.Ok("Quest created successfully.") 
        : Results.BadRequest("Failed to create quest. Check input data or ensure the quest does not already exist.");
}).WithName("CreateQuest").WithTags("Quests");

app.MapGet("/api/quests/available/{playerId:long}", async (long playerId, IQuestService questService) =>
{
    var quests = await questService.GetAvailableQuestsForPlayerAsync(playerId);
    return quests.Count > 0 
        ? Results.Ok(quests) 
        : Results.NotFound($"No available quests for player with ID {playerId}.");
}).WithName("GetAvailableQuestsForPlayer").WithTags("Quests");


app.Run();

