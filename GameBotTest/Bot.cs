using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GameBotTest;

public class Bot(ITelegramBotClient botClient, IConfiguration configuration)
{
    public async void Start()
    {
        var address = configuration["WEBHOOK_ADDRESS"];
        Console.WriteLine(address);
        if (address is null)
            throw new ArgumentNullException();
        var info = await botClient.GetWebhookInfoAsync();
        if (info.Url == address) return;
        await botClient.DeleteWebhookAsync();
        await botClient.SetWebhookAsync(address!, allowedUpdates: new List<UpdateType> { UpdateType.Message });
        Console.WriteLine(address);
    }
}