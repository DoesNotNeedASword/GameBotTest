using GameBotTest;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);

var token = builder.Configuration["BotSettings:token"];
builder.Services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient(token!));

builder.Services.AddSingleton<Bot>();
var app = builder.Build();

ITelegramBotClient botClient;
using (var scope = app.Services.CreateScope())
{
    var bot = scope.ServiceProvider.GetRequiredService<Bot>();
    botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    bot.Start();
}
app.MapPost("/webhook", async (Update e) =>
{
    if (e.Message?.Text == null) return Results.Ok();

    var chatId = e.Message.Chat.Id;
    var messageText = e.Message.Text.ToLower();

    var webAppUrl = builder.Configuration["BotSettings:webAppUrl"];
    var communityLink = builder.Configuration["BotSettings:communityLink"];

    if (messageText.StartsWith("/start"))
    {
        var code = GetCodeFromStartCommand(e.Message.Text);
        if (code == string.Empty)
        {
            await botClient.SendTextMessageAsync(e.Message.Chat.Id, "Invalid query param");
            return Results.Ok();
        }

        Console.WriteLine(code);
        var welcomeMessage =
            "Добро пожаловать в наш бот! Здесь вы можете запустить mini app, присоединиться к нашему сообществу и получать обновления.";
        var startKeyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("Запустить mini app"),
            new KeyboardButton("Присоединиться к комьюнити")
        })
        {
            ResizeKeyboard = true
        };
        await botClient.SendTextMessageAsync(chatId, welcomeMessage, replyMarkup: startKeyboard);
    }
    else if (messageText == ("запустить mini app"))
    {

        var earlyStageMessage = "Наш продукт находится в ранней стадии разработки. Мы будем рады вашим отзывам!";
        await botClient.SendTextMessageAsync(chatId, earlyStageMessage);

        var webAppKeyboard = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithWebApp("Запустить игру", new WebAppInfo { Url = webAppUrl })
        });
        await botClient.SendTextMessageAsync(chatId, "Нажмите кнопку ниже, чтобы начать игру:",
            replyMarkup: webAppKeyboard);
        return Results.Ok();
    }
    else if (messageText == ("присоединиться к комьюнити"))
    {

        var earlyStageMessage = "Наш продукт находится в ранней стадии разработки. Мы будем рады вашим отзывам! \nСсылка на группу: https://t.me/your_community_channel";
        await botClient.SendTextMessageAsync(chatId, earlyStageMessage);
        return Results.Ok();
    }
    return Results.Ok();
});

app.Run();

return;

string GetCodeFromStartCommand(string startCommand)
{
    var code = startCommand.Split(' ');
    return code.Length > 1 ? code[1] : string.Empty;
}
