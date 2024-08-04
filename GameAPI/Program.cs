using System.Text.Json;
using GameAPI.Options;
using GameAPI.Services;
using GameDomain.Interfaces;
using GameDomain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"];
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"];
const string cacheKey = "players";

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(provider =>
{
    var client = provider.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

BsonClassMap.RegisterClassMap<Player>(cm =>
{
    cm.AutoMap();
    cm.SetIgnoreExtraElements(true);  
});

var redisConfiguration = builder.Configuration["Redis:ConnectionString"];
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConfiguration;
    options.InstanceName = "SampleInstance";
});
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<ICacheService, CacheService>(provider => 
    new CacheService(provider.GetRequiredService<IDistributedCache>(), provider.GetRequiredService<PlayerService>()));
builder.Services.AddScoped<ILevelService, LevelService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseCors(options => options
    .WithOrigins("http://localhost:3001", "http://localhost:3002", 
        "http://localhost:3000", "http://localhost:8080", 
        "http://localhost:4200", "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());

app.MapControllers();


app.MapGet("/api/players", async (PlayerService playerService, ICacheService cacheService) =>
{
    var cachedPlayers = await cacheService.GetAsync(cacheKey);
    if (!string.IsNullOrEmpty(cachedPlayers))
    {
        return Results.Ok(JsonSerializer.Deserialize<List<Player>>(cachedPlayers));
    }

    var players = await playerService.GetAsync();
    await cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(players));
    return Results.Ok(players);
});

app.MapGet("/api/players/{id:long}", async (long id, PlayerService playerService) =>
{
    var player = await playerService.GetAsync(id);
    return Results.Ok(player);
});


app.MapPost("/api/players", async ([FromBody] Player player, PlayerService playerService, ICacheService cacheService) =>
{
    await playerService.CreateAsync(player);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.CreatedAtRoute("GetPlayer", new { id = player.Id }, player);
});

app.MapPut("/api/players/{id:long}", async (long id, Player playerIn, PlayerService playerService, ICacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);

    await playerService.UpdateAsync(id, playerIn);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.NoContent();
});

app.MapDelete("/api/players/{id:long}", async (long id, PlayerService playerService, ICacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);

    await playerService.RemoveAsync(id);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.NoContent();
});

app.MapGet("/api/leaders", async ([FromServices] ICacheService cacheService) =>
{
    try
    {
        var leaders = await cacheService.GetTopPlayersAsync();
        if (leaders.Count == 0)
        {
            return Results.NotFound("No leaders found.");
        }
        
        var playerList = leaders.Select(leader => new Player { Id = leader.Key, Rating = (int)leader.Value }).ToList();

        return Results.Ok(playerList);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});


app.Run();
