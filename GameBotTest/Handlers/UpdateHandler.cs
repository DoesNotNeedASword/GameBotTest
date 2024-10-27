using GameBotTest.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GameBotTest.Handlers;

public class UpdateHandler(CommandParser commandParser, CommandHandler commandHandler)
{
    public Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message?.Text == null) return Task.CompletedTask;

        var context = new Context
        {
            ChatId = update.Message.Chat.Id,
            MessageText = update.Message.Text,
            UserFirstName = update.Message.From!.FirstName,
            UserLastName = update.Message.From.LastName ?? string.Empty,
            UserName = update.Message.From.Username
        };
        var command = commandParser.Parse(context.MessageText.ToLower(), context);
        return commandHandler.HandleCommand(command, context);
    }
}
