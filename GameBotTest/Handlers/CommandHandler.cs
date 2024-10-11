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
                await _botClient.SendTextMessageAsync(context.ChatId, "Unknown command.");
            }
        }

        private async Task HandleNoRefCommand(Context context)
        {
            var noRefMessage = "You have joined without a referral code. Welcome! You can launch the mini app or join our community.";
            var startKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Launch mini app"),
                new KeyboardButton("Join community")
            })
            {
                ResizeKeyboard = true
            };
            var player = await _apiClient.RegisterPlayerAsync(context.ChatId, context.UserName ?? context.UserFirstName + " " + context.UserLastName);
            if (player is null)
            {
                await _botClient.SendTextMessageAsync(context.ChatId,
                    "Вы уже зарегистрированы. Запускайте бот и приятной игры :) \nYou are already registered. Launch the bot and enjoy the game :)", replyMarkup: startKeyboard);
                return;
            }
            await _botClient.SendTextMessageAsync(context.ChatId, noRefMessage, replyMarkup: startKeyboard);
        }

        private async Task HandleStartCommand(Context context)
        {
            var startKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Launch mini app"),
                new KeyboardButton("Join community")
            })
            {
                ResizeKeyboard = true
            };
            var player = await _apiClient.RegisterPlayerAsync(context.ChatId, context.UserName ?? context.UserFirstName + " " + context.UserLastName, context.RefId);
            if (player is null)
            {
                await _botClient.SendTextMessageAsync(context.ChatId,
                    "Вы уже зарегистрированы. Запускайте бот и приятной игры :) \nYou are already registered. Launch the bot and enjoy the game :)", replyMarkup: startKeyboard);;
                return;
            }
            var welcomeMessage = "Welcome to our bot! Here you can launch the mini app, join our community, and receive updates.";
            await _botClient.SendTextMessageAsync(context.ChatId, welcomeMessage, replyMarkup: startKeyboard);
        }

        private async Task HandleStartMiniAppCommand(Context context)
        {
            var earlyStageMessage = "Our product is in its early stage of development. We would love to hear your feedback!";
            await _botClient.SendTextMessageAsync(context.ChatId, earlyStageMessage);

            var webAppUrl = _configuration["WEB_APP_URL"];
            var webAppKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithWebApp("Start Game", new WebAppInfo { Url = webAppUrl! }),
            });
            await _botClient.SendTextMessageAsync(context.ChatId, $"Click the button below to start the game: {webAppUrl}", replyMarkup: webAppKeyboard);
        }

        private async Task HandleJoinCommunityCommand(Context context)
        {
            var communityLink = _configuration["COMMUNITY_LINK"];
            var id = await _apiClient.GetPlayerId(context.ChatId);
            var communityMessage = $"Our product is in its early stage of development. We would love to hear your feedback! \nGroup link: {communityLink+id}";
            await _botClient.SendTextMessageAsync(context.ChatId, communityMessage);
        }
    }
}
