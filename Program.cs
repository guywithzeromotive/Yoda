// Program.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Yoda_Bot.Config;
using Yoda_Bot.Firebase;
using Firebase.Database;
using Yoda_Bot.Services;
using Yoda_Bot.Utils;
using Yoda_Bot.TelegramBots;

namespace Yoda_Bot
{
    class Program
    {
        public static UserBot? userBot { get; private set; }
        public static StaffBot? staffBot { get; private set; }
        private static List<long> adminUserIds = new List<long>();

        static async Task Main(string[] args) // Changed to async Task Main
        {
            Console.WriteLine("Bot is starting...");

            try
            {
                var configLoader = new AppConfigLoader(); // Use AppConfigLoader
                AppConfig appConfig = configLoader.LoadConfig(); // Load full AppConfig

                adminUserIds = appConfig.AdminUserIds; // Get AdminUserIds from AppConfig

                FirebaseClient firebaseClient = FirebaseInitializer.InitializeFirebase(appConfig); // Pass AppConfig
                TicketService ticketService = new TicketService(firebaseClient);
                await ticketService.InitializeAsync();

                TelegramBotClient userBotClient = await TelegramBotInitializer.InitializeBot(appConfig.UserBotToken); // Use tokens from AppConfig
                TelegramBotClient staffBotClient = await TelegramBotInitializer.InitializeBot(appConfig.StaffBotToken);

               
                userBot = new UserBot(userBotClient, ticketService, appConfig); // Pass AppConfig only once
                staffBot = new StaffBot(staffBotClient, ticketService, appConfig, adminUserIds, userBot); // Pass AppConfig

                StartReceivingBots(userBotClient, staffBotClient);

                Console.WriteLine("Bot is running. Press any key to exit.");
                await Task.Run(() => Console.ReadKey());
            }
            catch (ConfigurationException ex)
            {
                Console.WriteLine($"Critical configuration error: {ex.Message}");
                // Stop the application if configuration is invalid
                Environment.Exit(1); // Exit with a non-zero exit code to indicate failure
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred during startup: {ex}");
                Environment.Exit(1);
            }
            finally
            {
                await StopBotsAndCleanup();
                Console.WriteLine("Bot has stopped.");
            }
        }


        private static List<long> LoadAdminUserIds()
        {
            string? adminUserIdsString = Environment.GetEnvironmentVariable("ADMIN_USER_IDS");
            if (string.IsNullOrEmpty(adminUserIdsString)) return new List<long>();

            return adminUserIdsString.Split(',')
                                     .Select(s => s.Trim())
                                     .Where(s => long.TryParse(s, out _))
                                     .Select(long.Parse)
                                     .ToList();
        }

        private static void StartReceivingBots(ITelegramBotClient userBotClient, ITelegramBotClient staffBotClient)
        {
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

            if (userBot != null)
            {
                userBotClient.StartReceiving(
                    updateHandler: userBot.HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: CancellationToken.None
                );
                Console.WriteLine("User Bot started receiving updates.");
            }
            else
            {
                throw new InvalidOperationException("UserBot is not initialized.");
            }
            if (staffBot != null)
            {
                staffBotClient.StartReceiving(
                   updateHandler: staffBot.HandleUpdateAsync,
                   errorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: CancellationToken.None
               );
                Console.WriteLine("Staff Bot started receiving updates.");
            }
            else
            {
                throw new InvalidOperationException("StaffBot is not initialized.");
            }
        }

        private static async Task StopBotsAndCleanup()
        {
            Console.WriteLine("\nStopping bots...");
            if (userBot?.botClient is IAsyncDisposable userBotAsyncDisposable) await userBotAsyncDisposable.DisposeAsync();
            if (staffBot?.botClient is IAsyncDisposable staffBotAsyncDisposable) await staffBotAsyncDisposable.DisposeAsync();
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
    }
}