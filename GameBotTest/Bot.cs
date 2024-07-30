using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GameBotTest;

public class Bot(ITelegramBotClient botClient, IConfiguration configuration)
{
    public async void Start()
    {
        var address = configuration["BotSettings:webhookAddress"];
        var info = await botClient.GetWebhookInfoAsync();
        if (info.Url == address) return;
        await botClient.DeleteWebhookAsync();
        await botClient.SetWebhookAsync(configuration["BotSettings:webhookAddress"]!, allowedUpdates: new List<UpdateType> { UpdateType.Message });
    }
}