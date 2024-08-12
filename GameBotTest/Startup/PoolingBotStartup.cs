using GameBotTest.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace GameBotTest.Startup;

public class PoolingBotStartup : IBotStartup
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateHandler _updateHandler;
    public PoolingBotStartup(ITelegramBotClient botClient, UpdateHandler updateHandler)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
    }

    public async Task StartAsync()
    {
        var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } // Receive all update types
        };
        await _botClient.DeleteWebhookAsync(cancellationToken: cts.Token);
        _botClient.StartReceiving(
            _updateHandler.HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Bot is starting with long-polling.");
        await Task.Delay(-1, cts.Token); // Keep the method running
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"An error occurred: {exception.Message}");
        return Task.CompletedTask;
    }
}
