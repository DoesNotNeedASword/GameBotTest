using System.Text;
using GameAPI.Options;
using GameAPI.Services;
using GameDomain.Interfaces;
using GameDomain.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace GameAPI.Extensions;

public static class BuilderExtension
{
    public static void AddServices(this WebApplicationBuilder builder)
    {
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
        builder.Services.AddScoped<ICarService, CarService>();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddLogging();
        var redisConfiguration = builder.Configuration["REDIS_CONNECTIONSTRING"];
        builder.Services.AddStackExchangeRedisCache(cacheOptions =>
        {
            cacheOptions.Configuration = redisConfiguration;
            cacheOptions.InstanceName = "SampleInstance";
        });
        builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
        builder.Services.AddScoped<IEnergyService, EnergyService>();
        builder.Services.AddScoped<IPlayerService, PlayerService>();
        builder.Services.AddScoped<ICacheService, CacheService>(provider => 
            new CacheService(provider.GetRequiredService<IDistributedCache>(), provider.GetRequiredService<IPlayerService>()));
        builder.Services.AddScoped<ILevelService, LevelService>();
        builder.Services.AddScoped<IQuestService, QuestService>();
    }
    public static IMongoDatabase AddMongoDb(this WebApplicationBuilder builder)
    {
        var mongoConnectionString = builder.Configuration["MONGODB_CONNECTIONSTRING"];
        var mongoDatabaseName = builder.Configuration["MONGODB_DATABASENAME"];
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
        BsonClassMap.RegisterClassMap<Region>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);  
        });
        BsonClassMap.RegisterClassMap<EnergyStation>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);  
        });
        BsonClassMap.RegisterClassMap<Car>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);  
        });
        
        return builder.Services.BuildServiceProvider().GetRequiredService<IMongoDatabase>();
  
    }
}