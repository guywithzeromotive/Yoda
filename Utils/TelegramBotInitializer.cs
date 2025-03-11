using System;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Yoda_Bot.Utils
{
    public static class TelegramBotInitializer
    {
        public static async Task<TelegramBotClient> InitializeBot(string botToken) // Return non-nullable and throw exception
        {
            if (string.IsNullOrEmpty(botToken))
            {
                throw new ConfigurationException("TELEGRAM_BOT_TOKEN environment variable is not set or is empty.");
            }

            TelegramBotClient botClient = new TelegramBotClient(botToken);

            try
            {
                var me = await botClient.GetMe(); // Add timeout
                Console.WriteLine($"Bot started as user: @{me.Username}");
                return botClient;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing bot: {ex}");
                throw new ConfigurationException("Failed to initialize Telegram Bot.", ex); // Throw exception
            }
        }
    }
}