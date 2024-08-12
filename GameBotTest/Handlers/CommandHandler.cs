using GameBotTest.GameHttpClient;
using GameBotTest.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GameBotTest.Handlers
{
    public class CommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IConfiguration _configuration;
        private readonly IGameApiClient _apiClient;

        private readonly Dictionary<string, Func<Context, Task>> _commandHandlers;

        public CommandHandler(ITelegramBotClient botClient, IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _botClient = botClient;
            _configuration = configuration;
            using (var scope = scopeFactory.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                _apiClient = serviceProvider.GetRequiredService<IGameApiClient>();
            }
            _commandHandlers = new Dictionary<string, Func<Context, Task>>
            {
                { "start", HandleStartCommand },
                { "startMiniApp", HandleStartMiniAppCommand },
                { "joinCommunity", HandleJoinCommunityCommand },
                { "noRef", HandleNoRefCommand },
            };
        }

        public async Task HandleCommand(string command, Context context)
        {
            if (_commandHandlers.TryGetValue(command, out var handler))
            {
                await handler(context);
            }
            else
            {
                await _botClient.SendTextMessageAsync(context.ChatId, "Неизвестная команда.");
            }
        }
        private async Task HandleNoRefCommand(Context context)
        {
            var noRefMessage = "Вы вошли без реферального кода. Добро пожаловать! Вы можете запустить mini app или присоединиться к нашему сообществу.";
            var startKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Запустить mini app"),
                new KeyboardButton("Присоединиться к комьюнити")
            })
            {
                ResizeKeyboard = true
            };
            await _botClient.SendTextMessageAsync(context.ChatId, noRefMessage, replyMarkup: startKeyboard);
        }

        private async Task HandleStartCommand(Context context)
        {
            var startKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Запустить mini app"),
                new KeyboardButton("Присоединиться к комьюнити")
            })
            {
                ResizeKeyboard = true
            };
            var player = await _apiClient.RegisterPlayerAsync(context.ChatId, context.UserName, context.RefId);
            if (player is null)
            {
                await _botClient.SendTextMessageAsync(context.ChatId, "Internal Error", replyMarkup: startKeyboard);
                return;
            }
            var welcomeMessage = "Добро пожаловать в наш бот! Здесь вы можете запустить mini app, присоединиться к нашему сообществу и получать обновления.";
            await _botClient.SendTextMessageAsync(context.ChatId, welcomeMessage, replyMarkup: startKeyboard);
        }

        private async Task HandleStartMiniAppCommand(Context context)
        {
            var earlyStageMessage = "Наш продукт находится в ранней стадии разработки. Мы будем рады вашим отзывам!";
            await _botClient.SendTextMessageAsync(context.ChatId, earlyStageMessage);

            var webAppUrl = _configuration["WEB_APP_URL"];
            var webAppKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithWebApp("Запустить игру", new WebAppInfo { Url = webAppUrl! }),
            });
            await _botClient.SendTextMessageAsync(context.ChatId, $"Нажмите кнопку ниже, чтобы начать игру: {webAppUrl}", replyMarkup: webAppKeyboard);
        }

        private async Task HandleJoinCommunityCommand(Context context)
        {
            var communityLink = _configuration["COMMUNITY_LINK"];
            var id = await _apiClient.GetPlayerId(context.ChatId);
            var communityMessage = $"Наш продукт находится в ранней стадии разработки. Мы будем рады вашим отзывам! \nСсылка на группу: {communityLink+id}";
            await _botClient.SendTextMessageAsync(context.ChatId, communityMessage);
        }
    }
}
