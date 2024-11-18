using GameDomain.Models.DTOs;
using Market.Interfaces;
using Market.Services;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redisConnectionString = builder.Configuration["Redis__ConnectionString"];
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IMarketService, MarketService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "MarketService API",
        Description = "API для управления выставлением машин на продажу"
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MarketService API v1");
        options.RoutePrefix = string.Empty; 
    });
}

app.MapGet("/", () => "MarketService is running!"); // Главный маршрут для проверки

app.MapPost("/api/marketplace/list", async (ListingDto listing, IMarketService marketService) =>
    {
        var result = await marketService.ListCarAsync(listing);
        return result ? Results.Ok("Car listed successfully") : Results.BadRequest("Failed to list car");
    })
    .WithName("ListCar")
    .WithTags("Marketplace");

app.MapGet("/api/marketplace/cars", async (IMarketService marketService) =>
    {
        var cars = await marketService.GetAllListingsAsync();
        return Results.Ok(cars);
    })
    .WithName("GetAllCars")
    .WithTags("Marketplace");

app.MapDelete("/api/marketplace/remove/{id}", async (string id, IMarketService marketService) =>
    {
        var result = await marketService.RemoveListingAsync(id);
        return result ? Results.Ok("Listing removed successfully") : Results.NotFound("Listing not found");
    })
    .WithName("RemoveCarListing")
    .WithTags("Marketplace");

app.Run();