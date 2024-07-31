using GameBotTest;
using GameBotTest.GameHttpClient;
using GameBotTest.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
var builder = WebApplication.CreateBuilder(args);

var token = builder.Configuration["BOT_TOKEN"];
builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(token!));

builder.Services.AddSingleton<CommandParser>();
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddScoped<HttpClient>();
builder.Services.AddSingleton<Bot>();
builder.Services.AddHttpClient<IGameApiClient, GameApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GameApi"]!);
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var bot = scope.ServiceProvider.GetRequiredService<Bot>();
    bot.Start();
}

app.MapPost("/webhook", async (CommandParser commandParser, CommandHandler commandHandler, Update update) =>
{
    if (update.Message?.Text == null) return Results.Ok();

    var context = new Context
    {
        ChatId = update.Message.Chat.Id,
        MessageText = update.Message.Text,
        UserName = update.Message.From.Username,
        UserFirstName = update.Message.From.FirstName,
        UserLastName = update.Message.From.LastName
    };

    var command = commandParser.Parse(context.MessageText.ToLower(), context);
    await commandHandler.HandleCommand(command, context);
    
    return Results.Ok();
});

app.Run();
