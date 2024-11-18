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
});

app.MapGet("/api/cars/{playerId:long}", async (long playerId, ICarService carsService) =>
{
    var cars = await carsService.GetCarsByPlayerAsync(playerId);
    return Results.Ok(cars);
});

app.MapGet("/api/car/{carId}", async (string carId, ICarService carsService) =>
{
    var car = await carsService.GetCarByIdAsync(carId);
    return car != null ? Results.Ok(car) : Results.NotFound("Car not found");
});

app.MapPut("/api/car/customize", async (CarCustomizationDto dto, ICarService carsService) =>
{
    var success = await carsService.CustomizeCarAsync(dto);
    return success ? Results.Ok("Car customized") : Results.NotFound("Car not found");
});

app.MapDelete("/api/car/{carId}", async (string carId, ICarService carsService) =>
{
    var success = await carsService.RemoveCarAsync(carId);
    return success ? Results.Ok("Car removed") : Results.NotFound("Car not found");
});

app.Run();

