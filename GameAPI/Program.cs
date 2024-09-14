using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using GameAPI.Models;
using GameAPI.Options;
using GameAPI.Services;
using GameDomain.Interfaces;
using GameDomain.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
            ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")!))
        };
    });
builder.Services.AddSingleton<JwtService>();
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("https://test-telegram-app.online") // Разрешённый источник
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddLogging();

var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"];
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"];
const string cacheKey = "players";
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);
    settings.MaxConnectionPoolSize = 500; 
    return new MongoClient(settings);
});
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
var constantKey = "WebAppData";
var botToken = builder.Configuration["BOT_TOKEN"]!;
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

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AllowSpecificOrigin");

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
}).RequireAuthorization();

app.MapGet("/api/players/{id:long}", async (long id, ICacheService cacheService) =>
{
    var player = await cacheService.GetPlayerAsync(id);
    return player != null ? Results.Ok(player) : Results.NotFound();
}).WithName("GetPlayer").AllowAnonymous();

app.MapPost("/api/players", async ([FromBody] Player player, PlayerService playerService, ICacheService cacheService) =>
{
    await playerService.CreateAsync(player);
    await cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(player)); 
    return Results.Ok(player);
}).AllowAnonymous();

app.MapPut("/api/players/{id:long}", async (long id, Player playerIn, PlayerService playerService,
    ICacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);
    if(player is null)
        return Results.NotFound();
    await playerService.UpdateAsync(id, playerIn);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.Ok();
}).AllowAnonymous();

app.MapPut("/api/players/{telegramId:long}/rating", async (long telegramId, [FromBody] int ratingChange,
    PlayerService playerService, ICacheService cacheService) =>
{
    var success = await playerService.UpdateRatingAsync(telegramId, ratingChange);

    if (success)
    {
        await cacheService.RemoveAsync(cacheKey);  
        return Results.Ok();
    }
    else
    {
        return Results.NotFound($"Player with Telegram ID {telegramId} not found.");
    }
}).AllowAnonymous();

app.MapDelete("/api/players/{id:long}", async (long id, PlayerService playerService,
    ICacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);

    await playerService.RemoveAsync(id);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.NoContent();
}).AllowAnonymous();

app.MapGet("/api/leaders", async ([FromServices] ICacheService cacheService) =>
{
    try
    {
        var leaders = await cacheService.GetTopPlayersAsync();
        if (leaders.Count == 0)
        {
            return Results.NotFound("No leaders found.");
        }
        
        var playerList = leaders.Select(leader => new Player { TelegramId = leader.Key,
            Rating = (int)leader.Value }).ToList();

        return Results.Ok(playerList);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).AllowAnonymous();


app.MapPost("/api/verify", async (HttpRequest request, ILogger<Program> logger) =>
{
    var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
    var payload = JsonSerializer.Deserialize<TgPayloadDto>(requestBody, options);

    if (payload == null || string.IsNullOrEmpty(payload.InitData))
    {
        return Results.BadRequest();
    }
    var data = HttpUtility.ParseQueryString(payload.InitData);

    return IsValidData(data, constantKey, botToken) ? Results.Ok(new { valid = true }) : Results.BadRequest();
}).AllowAnonymous();


app.MapPost("/api/login", async (LoginModel login, IConfiguration config, JwtService jwtService) =>
{
    if (login.Username != config["SERVICE_USERNAME"] || login.Password != config["SERVICE_PASSWORD"])
        return Results.Unauthorized();
    var token = jwtService.GenerateToken(login.Username);
    return Results.Ok(new { token });
}).AllowAnonymous();

app.Run();
return;

static byte[] Hmacsha256Hash(byte[] key, byte[] data)
{
    using var hmac = new HMACSHA256(key);
    return hmac.ComputeHash(data);
}

static bool IsValidData(NameValueCollection nameValueCollection, string key, string botToken)
{
    var dataDict = new SortedDictionary<string, string>(
        nameValueCollection.AllKeys.ToDictionary(x => x!, x => nameValueCollection[x]!),
        StringComparer.Ordinal);
    var dataCheckString = string.Join(
        '\n', dataDict.Where(x => x.Key != "hash")
            .Select(x => $"{x.Key}={x.Value}"));
    
    var secretKey = Hmacsha256Hash(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(botToken));
    var generatedHash = Hmacsha256Hash(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
    var actualHash = Convert.FromHexString(dataDict["hash"]);
    
    return actualHash.SequenceEqual(generatedHash);
}


public class TgPayloadDto
{
    [JsonPropertyName("init_data")]
    public string InitData { get; set; }
}