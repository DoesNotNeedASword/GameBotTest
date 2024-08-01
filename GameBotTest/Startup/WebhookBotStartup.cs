using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GameBotTest.Startup;

public class WebhookBotStartup(ITelegramBotClient botClient, IConfiguration configuration) : IBotStartup
{
    public async Task StartAsync()
    {
        var address = configuration["WEBHOOK_ADDRESS"];
        if (string.IsNullOrEmpty(address))
            throw new ArgumentNullException(nameof(address), "Webhook address must be set.");
        
        var info = await botClient.GetWebhookInfoAsync();
        if (info.Url != address)
        {
            await botClient.DeleteWebhookAsync();
            await botClient.SetWebhookAsync(address, allowedUpdates: new List<UpdateType> { UpdateType.Message });
        }
    }
}
