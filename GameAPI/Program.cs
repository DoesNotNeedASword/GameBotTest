using System.Text.Json;
using GameAPI.Models;
using GameAPI.Services;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

// Configure MongoDB
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"];
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"];
const string cacheKey = "players";

// Use the connection string and database name to configure your MongoDB client
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(provider =>
{
    var client = provider.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

BsonClassMap.RegisterClassMap<Player>(cm =>
{
    cm.AutoMap();
    cm.SetIgnoreExtraElements(true);  // Ignore fields in MongoDB that are not in the C# class
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "SampleInstance";
});
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<CacheService>();

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


app.MapGet("/api/players", async (PlayerService playerService, CacheService cacheService) =>
{
    var cachedPlayers = await cacheService.GetAsync(cacheKey);
    if (!string.IsNullOrEmpty(cachedPlayers))
    {
        return Results.Ok(JsonSerializer.Deserialize<List<Player>>(cachedPlayers));
    }

    var players = await playerService.GetAsync();
    cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(players));
    return Results.Ok(players);
});

app.MapGet("/api/players/{id:length(24)}", async (string id, PlayerService playerService) =>
{
    var player = await playerService.GetAsync(id);
    return Results.Ok(player);
})
.WithName("GetPlayer");

app.MapPost("/api/players", async (Player player, PlayerService playerService, CacheService cacheService) =>
{
    await playerService.CreateAsync(player);
    cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.CreatedAtRoute("GetPlayer", new { id = player.Id }, player);
});

app.MapPut("/api/players/{id:length(24)}", async (string id, Player playerIn, PlayerService playerService, CacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);

    await playerService.UpdateAsync(id, playerIn);
    cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.NoContent();
});

app.MapDelete("/api/players/{id:length(24)}", async (string id, PlayerService playerService, CacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);

    await playerService.RemoveAsync(id);
    cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.NoContent();
});

app.Run();
