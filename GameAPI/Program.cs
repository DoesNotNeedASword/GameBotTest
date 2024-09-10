using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();
builder.Services.AddLogging();

var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"];
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"];
const string cacheKey = "players";

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
app.UseCors(options => options
    .WithOrigins("https://test-telegram-app.online") // Разрешить только конкретный домен
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
}).RequireAuthorization();

app.MapGet("/api/players/{id:long}", async (long id, ICacheService cacheService) =>
{
    var player = await cacheService.GetPlayerAsync(id);
    return player != null ? Results.Ok(player) : Results.NotFound();
}).WithName("GetPlayer");

app.MapPost("/api/players", async ([FromBody] Player player, PlayerService playerService, ICacheService cacheService) =>
{
    await playerService.CreateAsync(player);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.CreatedAtRoute("GetPlayer", new { id = player.TelegramId }, player);
});

app.MapPut("/api/players/{id:long}", async (long id, Player playerIn, PlayerService playerService, ICacheService cacheService) =>
{
    var player = await playerService.GetAsync(id);
    
    await playerService.UpdateAsync(id, playerIn);
    await cacheService.RemoveAsync(cacheKey);  // Invalidate cache
    return Results.NotFound();
});

app.MapPut("/api/players/{telegramId:long}/rating", async (long telegramId, [FromBody] int ratingChange, PlayerService playerService, ICacheService cacheService) =>
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
        
        var playerList = leaders.Select(leader => new Player { TelegramId = leader.Key, Rating = (int)leader.Value }).ToList();

        return Results.Ok(playerList);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/verify", async (HttpRequest request, ILogger<Program> logger) =>
{
    try
    {
        string initData;

        // Check the Content-Type of the request
        if (request.ContentType != null && request.ContentType.Contains("application/json"))
        {
            // Read JSON data
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            var jsonData = JsonDocument.Parse(body);
            initData = jsonData.RootElement.GetProperty("init_data").GetString();
        }
        else if (request.ContentType != null && request.ContentType.Contains("application/x-www-form-urlencoded"))
        {
            // Read form data
            var formData = await request.ReadFormAsync();
            initData = formData["init_data"].ToString();
        }
        else
        {
            logger.LogWarning("Unsupported content type: {ContentType}", request.ContentType);
            return Results.BadRequest("Unsupported content type.");
        }

        // Log the received initData
        logger.LogInformation("Received init_data: {initData}", initData);

        var parsedData = QueryHelpers.ParseQuery(initData);

        // Log all parsed fields
        foreach (var field in parsedData)
        {
            logger.LogInformation("Field: {key} = {value}", field.Key, field.Value);
        }

        if (!parsedData.TryGetValue("hash", out var hashStr) || string.IsNullOrEmpty(hashStr))
        {
            logger.LogWarning("No hash found or hash is empty.");
            return Results.Json(new { valid = false });
        }

        // Prepare the data for HMAC
        var sortedData = parsedData.Where(p => p.Key != "hash")
            .Select(p => $"{p.Key}={p.Value}")
            .OrderBy(p => p)
            .ToList();
        var initDataStr = string.Join("\n", sortedData);

        // Log the initData string used for HMAC
        logger.LogInformation("Prepared init_data string for HMAC: {initDataStr}", initDataStr);

        // Generate HMAC
        var secretKey = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData" + builder.Configuration["BOT_TOKEN"]));
        var dataCheck = secretKey.ComputeHash(Encoding.UTF8.GetBytes(initDataStr));

        // Generate the computed hash
        var computedHash = BitConverter.ToString(dataCheck).Replace("-", "").ToLower();

        // Log the computed hash and provided hash
        logger.LogInformation("Provided hash: {hashStr}", hashStr);
        logger.LogInformation("Computed hash: {computedHash}", computedHash);

        // Validate the hash
        var isValid = computedHash.Equals(hashStr.ToString(), StringComparison.CurrentCultureIgnoreCase);
        logger.LogInformation("Verification result: {isValid}", isValid);

        return Results.Json(new { valid = isValid });
    }
    catch (Exception ex)
    {
        // Log the exception
        logger.LogError(ex, "Verification failed with an error.");
        return Results.BadRequest("Verification failed");
    }
});



app.MapPost("/api/login", async (LoginModel login, IConfiguration config) =>
{
    var authService = new AuthService(config);

    if (!IsValidUser(login)) return Results.Unauthorized();
    var token = authService.GenerateToken(login.Username);
    return Results.Ok(new { token });

}).AllowAnonymous();

bool IsValidUser(LoginModel login)
{
    return login is { Username: "test", Password: "test" };
}

app.Run();
