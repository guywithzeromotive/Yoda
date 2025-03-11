// Yoda_Bot/Utils/AppConfigLoader.cs
using System;
using System.IO;
using System.Text.Json;
using dotenv.net;
using Yoda_Bot.Config;
using System.Collections.Generic;
using System.Linq;

namespace Yoda_Bot.Utils
{
    public class AppConfigLoader
    {
        public AppConfig LoadConfig()
        {
            try
            {
                DotEnv.Load(); // Load .env file
                AppConfig config = LoadEnvConfig();
                LoadBotConfig(config); // Load bot_config.json and merge into config
                ValidateConfig(config); // Validate required configurations are loaded
                return config;
            }
            catch (ConfigurationException ex)
            {
                Console.WriteLine($"Configuration Error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error loading application config: {ex}");
                throw new ConfigurationException("An unexpected error occurred while loading application configuration.", ex);
            }
        }

        private AppConfig LoadEnvConfig()
        {
            try
            {
                return new AppConfig
                {
                    LogsChannelId = LoadLongVariable("LOGS_CHANNEL_ID", "Logs Channel ID"),
                    StaffChatId = LoadLongVariable("STAFF_CHAT_ID", "Staff Chat ID"),
                    FirebaseDatabaseUrl = LoadStringVariable("FIREBASE_DATABASE_URL", "Firebase Database URL"),
                    FirebaseServiceAccountKeyPath = LoadStringVariable("FIREBASE_SERVICE_ACCOUNT_KEY_PATH", "Firebase Service Account Key Path"),
                    UserBotToken = LoadStringVariable("USER_BOT_TOKEN", "User Bot Token"),
                    StaffBotToken = LoadStringVariable("STAFF_BOT_TOKEN", "Staff Bot Token"),
                    AdminUserIds = LoadAdminUserIdsVariable("ADMIN_USER_IDS", "Admin User IDs")
                };
            }
            catch (ConfigurationException)
            {
                // Let the calling LoadConfig method handle and re-throw
                throw;
            }
        }


        private void LoadBotConfig(AppConfig config)
        {
            string configFilePath = "/app/bot_config.json";
            Console.WriteLine($"Looking for config file at: {configFilePath}");
            Console.WriteLine($"Does file exist? {File.Exists(configFilePath)}");

            try
            {
                if (!File.Exists(configFilePath))
                {
                    throw new ConfigurationException($"Configuration file '{configFilePath}' not found.");
                }

                string jsonString = File.ReadAllText(configFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                Dictionary<string, string>? botConfigFile = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString, options);

                if (botConfigFile != null)
                {
                    // Map properties from dictionary to AppConfig - using dictionary lookup
                    config.WelcomeMessage = GetConfigValue(botConfigFile, "welcomeMessage", config.WelcomeMessage);
                    config.MenuMessage = GetConfigValue(botConfigFile, "menuMessage", config.MenuMessage);
                    config.ServicesMessage = GetConfigValue(botConfigFile, "servicesMessage", config.ServicesMessage);
                    config.SocialsMessage = GetConfigValue(botConfigFile, "socialsMessage", config.SocialsMessage);
                    config.TicketCreatedMessage = GetConfigValue(botConfigFile, "ticketCreatedMessage", config.TicketCreatedMessage);
                    config.TicketClosedMessageUser = GetConfigValue(botConfigFile, "ticketClosedMessageUser", config.TicketClosedMessageUser);
                    config.ContactSupportButton = GetConfigValue(botConfigFile, "contactSupportButton", config.ContactSupportButton);
                    config.MenuButton = GetConfigValue(botConfigFile, "menuButton", config.MenuButton);
                    config.LanguageButton = GetConfigValue(botConfigFile, "languageButton", config.LanguageButton);
                    config.EnglishButton = GetConfigValue(botConfigFile, "englishButton", config.EnglishButton);
                    config.AmharicButton = GetConfigValue(botConfigFile, "amharicButton", config.AmharicButton);
                    config.SelectLanguageMessage = GetConfigValue(botConfigFile, "selectLanguageMessage", config.SelectLanguageMessage);
                    config.LanguageSetMessage = GetConfigValue(botConfigFile, "languageSetMessage", config.LanguageSetMessage);
                    config.MessagingOutsideActiveTicket = GetConfigValue(botConfigFile, "messagingOutsideActiveTicket", config.MessagingOutsideActiveTicket);
                    config.MessageHistory = GetConfigValue(botConfigFile, "messageHistory", config.MessageHistory);
                    config.StaffReplied = GetConfigValue(botConfigFile, "staffReplied", config.StaffReplied);
                    config.NoMessagesYet = GetConfigValue(botConfigFile, "noMessagesYet", config.NoMessagesYet);
                    config.ErrorOccurredMessage = GetConfigValue(botConfigFile, "errorOccurredMessage", config.ErrorOccurredMessage);
                    config.FailedToCreateTicketMessage = GetConfigValue(botConfigFile, "failedToCreateTicketMessage", config.FailedToCreateTicketMessage);
                    config.FailedToCloseTicketMessage = GetConfigValue(botConfigFile, "failedToCloseTicketMessage", config.FailedToCloseTicketMessage);
                    config.InvalidSelectionMessage = GetConfigValue(botConfigFile, "invalidSelectionMessage", config.InvalidSelectionMessage);
                    config.StaffMenuMessage = GetConfigValue(botConfigFile, "staffMenuMessage", config.StaffMenuMessage);
                    config.ViewTicketsOptionsMessage = GetConfigValue(botConfigFile, "viewTicketsOptionsMessage", config.ViewTicketsOptionsMessage);
                    config.NoOpenTicketsMessage = GetConfigValue(botConfigFile, "noOpenTicketsMessage", config.NoOpenTicketsMessage);
                    config.NoClosedTicketsMessage = GetConfigValue(botConfigFile, "noClosedTicketsMessage", config.NoClosedTicketsMessage);
                    config.AllTicketsMessage = GetConfigValue(botConfigFile, "allTicketsMessage", config.AllTicketsMessage);
                    config.OpenTicketsMessage = GetConfigValue(botConfigFile, "openTicketsMessage", config.OpenTicketsMessage);
                    config.ClosedTicketsMessage = GetConfigValue(botConfigFile, "closedTicketsMessage", config.ClosedTicketsMessage);
                    config.InvalidTicketTypeMessage = GetConfigValue(botConfigFile, "invalidTicketTypeMessage", config.InvalidTicketTypeMessage);
                    config.TicketDetailsMessage = GetConfigValue(botConfigFile, "ticketDetailsMessage", config.TicketDetailsMessage);
                    config.TicketIdMessage = GetConfigValue(botConfigFile, "ticketIdMessage", config.TicketIdMessage);
                    config.UserIdMessage = GetConfigValue(botConfigFile, "userIdMessage", config.UserIdMessage);
                    config.CreatedMessage = GetConfigValue(botConfigFile, "createdMessage", config.CreatedMessage);
                    config.MessageHistoryMessage = GetConfigValue(botConfigFile, "messageHistoryMessage", config.MessageHistoryMessage);
                    config.NoMessagesYetMessage = GetConfigValue(botConfigFile, "noMessagesYetMessage", config.NoMessagesYetMessage);
                    config.ReplyToUserMessage = GetConfigValue(botConfigFile, "replyToUserMessage", config.ReplyToUserMessage);
                    config.SwitchTicket = GetConfigValue(botConfigFile, "switchTicket", config.SwitchTicket);
                    config.CloseTicketMessage = GetConfigValue(botConfigFile, "closeTicketMessage", config.CloseTicketMessage);
                    config.DeleteTicketMessage = GetConfigValue(botConfigFile, "deleteTicketMessage", config.DeleteTicketMessage);
                    config.ReopenTicketMessage = GetConfigValue(botConfigFile, "reopenTicketMessage", config.ReopenTicketMessage);
                    config.CancelButton = GetConfigValue(botConfigFile, "cancelButton", config.CancelButton);
                    config.ErrorRetrievingTicketDetailsMessage = GetConfigValue(botConfigFile, "errorRetrievingTicketDetailsMessage", config.ErrorRetrievingTicketDetailsMessage);
                    config.InvalidTicketNumberSelectedMessage = GetConfigValue(botConfigFile, "invalidTicketNumberSelectedMessage", config.InvalidTicketNumberSelectedMessage);
                    config.TelegramApiErrorMessage = GetConfigValue(botConfigFile, "telegramApiErrorMessage", config.TelegramApiErrorMessage);
                    config.ErrorCouldNotFindTicketDataMessage = GetConfigValue(botConfigFile, "errorCouldNotFindTicketDataMessage", config.ErrorCouldNotFindTicketDataMessage);
                    config.ErrorCouldNotFindTicketDataForReplyMessage = GetConfigValue(botConfigFile, "errorCouldNotFindTicketDataForReplyMessage", config.ErrorCouldNotFindTicketDataForReplyMessage);
                    config.YouAreNowHandlingTicketMessage = GetConfigValue(botConfigFile, "youAreNowHandlingTicketMessage", config.YouAreNowHandlingTicketMessage);
                    config.ReplyToUserByTicketIdMessage = GetConfigValue(botConfigFile, "replyToUserByTicketIdMessage", config.ReplyToUserByTicketIdMessage);
                    config.TelegramApiErrorInitiatingReplyMessage = GetConfigValue(botConfigFile, "telegramApiErrorInitiatingReplyMessage", config.TelegramApiErrorInitiatingReplyMessage);
                    config.ErrorInitiatingReplyToUserMessage = GetConfigValue(botConfigFile, "errorInitiatingReplyToUserMessage", config.ErrorInitiatingReplyToUserMessage);
                    config.StaffReplyMessageEmptyMessage = GetConfigValue(botConfigFile, "staffReplyMessageEmptyMessage", config.StaffReplyMessageEmptyMessage);
                    config.InvalidReplyContextMessage = GetConfigValue(botConfigFile, "invalidReplyContextMessage", config.InvalidReplyContextMessage);
                    config.CouldNotParseTicketNumberMessage = GetConfigValue(botConfigFile, "couldNotParseTicketNumberMessage", config.CouldNotParseTicketNumberMessage);
                    config.ErrorCouldNotFindTicketDataToSendReplyMessage = GetConfigValue(botConfigFile, "errorCouldNotFindTicketDataToSendReplyMessage", config.ErrorCouldNotFindTicketDataToSendReplyMessage);
                    config.InvalidTicketNumberInReplyContextMessage = GetConfigValue(botConfigFile, "invalidTicketNumberInReplyContextMessage", config.InvalidTicketNumberInReplyContextMessage);
                    config.TelegramApiErrorProcessingStaffReplyMessage = GetConfigValue(botConfigFile, "telegramApiErrorProcessingStaffReplyMessage", config.TelegramApiErrorProcessingStaffReplyMessage);
                    config.ErrorProcessingStaffReplyMessage = GetConfigValue(botConfigFile, "errorProcessingStaffReplyMessage", config.ErrorProcessingStaffReplyMessage);
                    config.ReplySentToUserMessage = GetConfigValue(botConfigFile, "replySentToUserMessage", config.ReplySentToUserMessage);
                    config.TicketClosedMessage = GetConfigValue(botConfigFile, "ticketClosedMessage", config.TicketClosedMessage);
                    config.ErrorCannotEditMessageAfterTicketClosureMessage = GetConfigValue(botConfigFile, "errorCannotEditMessageAfterTicketClosureMessage", config.ErrorCannotEditMessageAfterTicketClosureMessage);
                    config.TelegramApiErrorClosingTicketMessage = GetConfigValue(botConfigFile, "telegramApiErrorClosingTicketMessage", config.TelegramApiErrorClosingTicketMessage);
                    config.ErrorClosingTicketMessage = GetConfigValue(botConfigFile, "errorClosingTicketMessage", config.ErrorClosingTicketMessage);
                    config.TicketDeletedMessage = GetConfigValue(botConfigFile, "ticketDeletedMessage", config.TicketDeletedMessage);
                    config.ErrorCannotEditMessageAfterTicketDeletionMessage = GetConfigValue(botConfigFile, "errorCannotEditMessageAfterTicketDeletionMessage", config.ErrorCannotEditMessageAfterTicketDeletionMessage);
                    config.TelegramApiErrorTerminatingTicketMessage = GetConfigValue(botConfigFile, "telegramApiErrorTerminatingTicketMessage", config.TelegramApiErrorTerminatingTicketMessage);
                    config.ErrorTerminatingTicketMessage = GetConfigValue(botConfigFile, "errorTerminatingTicketMessage", config.ErrorTerminatingTicketMessage);
                    config.TicketReopenedMessage = GetConfigValue(botConfigFile, "ticketReopenedMessage", config.TicketReopenedMessage);
                    config.ErrorCannotEditMessageAfterTicketReopeningMessage = GetConfigValue(botConfigFile, "errorCannotEditMessageAfterTicketReopeningMessage", config.ErrorCannotEditMessageAfterTicketReopeningMessage);
                    config.TelegramApiErrorReopeningTicketMessage = GetConfigValue(botConfigFile, "telegramApiErrorReopeningTicketMessage", config.TelegramApiErrorReopeningTicketMessage);
                    config.ErrorReopeningTicketMessage = GetConfigValue(botConfigFile, "errorReopeningTicketMessage", config.ErrorReopeningTicketMessage);
                    config.TicketTranscriptSentToLogsMessage = GetConfigValue(botConfigFile, "ticketTranscriptSentToLogsMessage", config.TicketTranscriptSentToLogsMessage);
                    config.ErrorCouldNotFindTicketDataToGenerateTranscriptMessage = GetConfigValue(botConfigFile, "errorCouldNotFindTicketDataToGenerateTranscriptMessage", config.ErrorCouldNotFindTicketDataToGenerateTranscriptMessage);
                    config.TelegramApiErrorSendingTicketTranscriptMessage = GetConfigValue(botConfigFile, "telegramApiErrorSendingTicketTranscriptMessage", config.TelegramApiErrorSendingTicketTranscriptMessage);
                    config.ErrorSendingTicketTranscriptMessage = GetConfigValue(botConfigFile, "errorSendingTicketTranscriptMessage", config.ErrorSendingTicketTranscriptMessage);
                    config.YouAreNoLongerHandlingTicketMessage = GetConfigValue(botConfigFile, "youAreNoLongerHandlingTicketMessage", config.YouAreNoLongerHandlingTicketMessage);
                    config.YouAreNotCurrentlyHandlingTicketMessage = GetConfigValue(botConfigFile, "youAreNotCurrentlyHandlingTicketMessage", config.YouAreNotCurrentlyHandlingTicketMessage);
                    config.SendCloseTicketConfirmationMessage = GetConfigValue(botConfigFile, "sendCloseTicketConfirmationMessage", config.SendCloseTicketConfirmationMessage);
                    config.TelegramApiErrorSendingCloseTicketConfirmationMessage = GetConfigValue(botConfigFile, "telegramApiErrorSendingCloseTicketConfirmationMessage", config.TelegramApiErrorSendingCloseTicketConfirmationMessage);
                    config.ErrorSendingCloseTicketConfirmationMessage = GetConfigValue(botConfigFile, "errorSendingCloseTicketConfirmationMessage", config.ErrorSendingCloseTicketConfirmationMessage);
                    config.SendReopenTicketConfirmationMessage = GetConfigValue(botConfigFile, "sendReopenTicketConfirmationMessage", config.SendReopenTicketConfirmationMessage);
                    config.TelegramApiErrorSendingReopenTicketConfirmationMessage = GetConfigValue(botConfigFile, "telegramApiErrorSendingReopenTicketConfirmationMessage", config.TelegramApiErrorSendingReopenTicketConfirmationMessage);
                    config.ErrorSendingReopenTicketConfirmationMessage = GetConfigValue(botConfigFile, "errorSendingReopenTicketConfirmationMessage", config.ErrorSendingReopenTicketConfirmationMessage);
                    config.SendDeleteTicketConfirmationMessage = GetConfigValue(botConfigFile, "sendDeleteTicketConfirmationMessage", config.SendDeleteTicketConfirmationMessage);
                    config.TelegramApiErrorSendingDeleteTicketConfirmationMessage = GetConfigValue(botConfigFile, "telegramApiErrorSendingDeleteTicketConfirmationMessage", config.TelegramApiErrorSendingDeleteTicketConfirmationMessage);
                    config.ErrorSendingDeleteTicketConfirmationMessage = GetConfigValue(botConfigFile, "errorSendingDeleteTicketConfirmationMessage", config.ErrorSendingDeleteTicketConfirmationMessage);
                    config.SearchTicketsMessage = GetConfigValue(botConfigFile, "searchTicketsMessage", config.SearchTicketsMessage);
                    config.SearchResultsMessage = GetConfigValue(botConfigFile, "searchResultsMessage", config.SearchResultsMessage);
                    config.NoTicketsFoundMessage = GetConfigValue(botConfigFile, "noTicketsFoundMessage", config.NoTicketsFoundMessage);
                    config.TelegramApiErrorDuringTicketSearchMessage = GetConfigValue(botConfigFile, "telegramApiErrorDuringTicketSearchMessage", config.TelegramApiErrorDuringTicketSearchMessage);
                    config.ErrorDuringTicketSearchMessage = GetConfigValue(botConfigFile, "errorDuringTicketSearchMessage", config.ErrorDuringTicketSearchMessage);
                    config.AdminBroadcastMessage = GetConfigValue(botConfigFile, "adminBroadcastMessage", config.AdminBroadcastMessage);
                    config.TelegramApiErrorBroadcastingMessage = GetConfigValue(botConfigFile, "telegramApiErrorBroadcastingMessage", config.TelegramApiErrorBroadcastingMessage);
                    config.ErrorBroadcastingMessage = GetConfigValue(botConfigFile, "errorBroadcastingMessage", config.ErrorBroadcastingMessage);
                    config.UsageAdminBroadcastMessage = GetConfigValue(botConfigFile, "usageAdminBroadcastMessage", config.UsageAdminBroadcastMessage);
                    config.UsageDeleteTicketMessage = GetConfigValue(botConfigFile, "usageDeleteTicketMessage", config.UsageDeleteTicketMessage);
                    config.UnknownAdminCommandMessage = GetConfigValue(botConfigFile, "unknownAdminCommandMessage", config.UnknownAdminCommandMessage);
                    config.AvailableAdminCommandsMessage = GetConfigValue(botConfigFile, "availableAdminCommandsMessage", config.AvailableAdminCommandsMessage);
                    config.StaffRepliedToYourTicketMessage = GetConfigValue(botConfigFile, "staffRepliedToYourTicketMessage", config.StaffRepliedToYourTicketMessage);
                    config.UnsupportedMessageTypeReceivedFromStaffMessage = GetConfigValue(botConfigFile, "unsupportedMessageTypeReceivedFromStaffMessage", config.UnsupportedMessageTypeReceivedFromStaffMessage);
                    config.UserSentMessagePrefix = GetConfigValue(botConfigFile, "userSentMessagePrefix", config.UserSentMessagePrefix);
                    config.ImageErrorMessage = GetConfigValue(botConfigFile, "imageErrorMessage", config.ImageErrorMessage);
                    config.AudioErrorMessage = GetConfigValue(botConfigFile, "audioErrorMessage", config.AudioErrorMessage);
                    config.VoiceErrorMessage = GetConfigValue(botConfigFile, "voiceErrorMessage", config.VoiceErrorMessage);
                    config.VideoErrorMessage = GetConfigValue(botConfigFile, "videoErrorMessage", config.VideoErrorMessage);
                    config.DocumentErrorMessage = GetConfigValue(botConfigFile, "documentErrorMessage", config.DocumentErrorMessage);
                    config.UnsupportedMessageTypeMessage = GetConfigValue(botConfigFile, "unsupportedMessageTypeMessage", config.UnsupportedMessageTypeMessage);
                    config.LogTicketCreated = GetConfigValue(botConfigFile, "logTicketCreated", config.LogTicketCreated);
                    config.LogTicketClosedByStaff = GetConfigValue(botConfigFile, "logTicketClosedByStaff", config.LogTicketClosedByStaff);
                    config.LogTicketReopenedByStaff = GetConfigValue(botConfigFile, "logTicketReopenedByStaff", config.LogTicketReopenedByStaff);
                    config.LogTicketDeletedByStaff = GetConfigValue(botConfigFile, "logTicketDeletedByStaff", config.LogTicketDeletedByStaff);
                    config.LogTicketTranscript = GetConfigValue(botConfigFile, "logTicketTranscript", config.LogTicketTranscript);
                    config.LogTicketDeletedWithTranscript = GetConfigValue(botConfigFile, "logTicketDeletedWithTranscript", config.LogTicketDeletedWithTranscript);
                }
            }
            catch (JsonException ex)
            {
                throw new ConfigurationException($"Failed to parse JSON configuration from '{configFilePath}'. Invalid JSON format.\n{ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new ConfigurationException($"Error reading configuration file '{configFilePath}': {ex.Message}", ex);
            }
            catch (ConfigurationException)
            {
                throw; // Re-throw ConfigurationException to be caught by the caller
            }
            catch (Exception ex)
            {
                throw new ConfigurationException($"Unexpected error loading bot configuration: {ex.Message}", ex);
            }
        }

        private string GetConfigValue(Dictionary<string, string> configDict, string key, string defaultValue)
        {
            return configDict.TryGetValue(key, out string? value) ? value : defaultValue;
        }


        private void ValidateConfig(AppConfig config)
        {
            if (config == null)
            {
                throw new ConfigurationException("Failed to load application configuration. AppConfig is null.");
            }
            if (config.LogsChannelId == 0)
            {
                throw new ConfigurationException("Logs Channel ID is not configured.");
            }
             if (config.StaffChatId == 0)
            {
                throw new ConfigurationException("Staff Chat ID is not configured.");
            }
            if (string.IsNullOrEmpty(config.FirebaseDatabaseUrl))
            {
                throw new ConfigurationException("Firebase Database URL is not configured.");
            }
            if (string.IsNullOrEmpty(config.FirebaseServiceAccountKeyPath))
            {
                throw new ConfigurationException("Firebase Service Account Key Path is not configured.");
            }
            if (string.IsNullOrEmpty(config.UserBotToken))
            {
                throw new ConfigurationException("User Bot Token is not configured.");
            }
            if (string.IsNullOrEmpty(config.StaffBotToken))
            {
                throw new ConfigurationException("Staff Bot Token is not configured.");
            }
            if (config.AdminUserIds == null || config.AdminUserIds.Count == 0)
            {
                Console.WriteLine("Warning: Admin User IDs are not configured. Admin commands will be disabled.");
                // Not throwing exception for AdminUserIds, just a warning.
            }
            // Add more validation rules for other critical settings if needed.
        }


        private string LoadStringVariable(string variableName, string displayName)
        {
            string? valueStr = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(valueStr))
            {
                throw new ConfigurationException($"Required environment variable '{variableName}' ('{displayName}') is missing or empty in .env file.");
            }
            return valueStr;
        }

        private long LoadLongVariable(string variableName, string displayName)
        {
            string? valueStr = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(valueStr))
            {
                throw new ConfigurationException($"Required environment variable '{variableName}' ('{displayName}') is missing or empty in .env file.");
            }
            if (!long.TryParse(valueStr, out long parsedValue))
            {
                throw new ConfigurationException($"'{displayName}' environment variable ('{variableName}') is not a valid number. Value in .env: '{valueStr}'.");
            }
            return parsedValue;
        }
        private List<long> LoadAdminUserIdsVariable(string variableName, string displayName)
        {
            string? adminUserIdsString = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(adminUserIdsString)) return new List<long>(); // Return empty list if not configured

            return adminUserIdsString.Split(',')
                                     .Select(s => s.Trim())
                                     .Where(s => long.TryParse(s, out _))
                                     .Select(long.Parse)
                                     .ToList();
        }
    }
}