using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yoda_Bot.Config;
using Yoda_Bot.Services;
using Yoda_Bot.Models;
using Yoda_Bot.Utils;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using Yoda_Bot.Helpers;
using dotenv.net;

namespace Yoda_Bot.TelegramBots
{
    public class UserBot
    {
        public readonly ITelegramBotClient botClient;
        private readonly TicketService ticketService;
        //private readonlyS Config?S Config; 
        private LanguageParser? _languageParser;
        private readonly AppConfig _appConfig;

        // UserBot.cs (Constructor)
public UserBot(ITelegramBotClient botClient, TicketService ticketService, AppConfig appConfig) // RemovedS Config parameter
{
    this.botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
    this.ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
    this._appConfig = appConfig; // Assign AppConfig
    // thisS.Config =S Config; // REMOVE this line
    _languageParser = new LanguageParser("en");
}
        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                long userId = 0; // Store userId here. Common to both message and callbackquery
                if (update.Message is { } message) // No local variable declaration here!
                {
                    if (message.From != null)
                    {
                        userId = message.From.Id;  // Get userId *once*.
                    }
                    else
                    {
                        Console.WriteLine("Error: message.From is null.");
                        return;
                    }
                    // Load user data (including language preference).  Do this *once* per update.
                    UserData? userData = await ticketService.GetUserDataAsync(userId);
                    string userLanguage = userData?.Language ?? "en"; // Default to English

                    // Initialize the language parser with the user's language.
                    _languageParser = new LanguageParser(userLanguage);
                    await HandleMessageAsync(message, userId, cancellationToken); // Pass userId
                }
                else if (update.CallbackQuery is { } callbackQuery)
                {
                    userId = callbackQuery.From.Id;
                    UserData? userData = await ticketService.GetUserDataAsync(userId);
                    string userLanguage = userData?.Language ?? "en"; // Default to English
                    _languageParser = new LanguageParser(userLanguage); //Initilize here based on user data.
                    await HandleCallbackQueryAsync(callbackQuery, userId, cancellationToken); // Pass userId

                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleUpdateAsync (UserBot): {ex}");
            }
        }
        private async Task<string?> GetActiveTicketIdForChatId(long chatId)
        {
            var openTickets = await ticketService.GetOpenTicketsAsync();
            var userOpenTicket = openTickets.FirstOrDefault(t => t.ChatId == chatId);
            return userOpenTicket?.TicketId;

        }


        private async Task HandleMessageAsync(Message message, long userId, CancellationToken cancellationToken) // Added userId
        {
            long chatId = message.Chat.Id;
            string? text = message.Text;

            // Check if the language parser is initialized.
            if (_languageParser == null)
            {
                Console.WriteLine("Error: LanguageParser is not initialized in UserBot.");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ An internal error occurred. Please try again later.", cancellationToken: cancellationToken);
                return;
            }

            try
            {
                UserData? userData = await ticketService.GetUserDataAsync(userId); // Get UserData
                if (userData == null || userData.Language == null)
                {
                    // First-time user: Prompt for language selection
                    if (text == "/start" || text == "/menu" || text?.ToLower() == "language")
                    {
                        await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("selectLanguageMessage"), replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("englishButton"), "set_language_en") },
                            new[] { InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("amharicButton"), "set_language_am") }
                        }), cancellationToken: cancellationToken);
                        return; // Important:  Stop processing until language is chosen.
                    }
                    else
                    {
                        //If user sends anything with out setting language
                        await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("selectLanguageMessage"), replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("englishButton"), "set_language_en") },
                            new[] { InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("amharicButton"), "set_language_am") }
                        }), cancellationToken: cancellationToken);
                        return; // Important:  Stop processing until language is chosen.

                    }
                }

                // User has selected a language. Proceed.
                if (ticketService.ActiveTicketsChatIdToTicketIdMap.ContainsKey(chatId)) // Use the *in-memory* map!
                {
                    // User has an ACTIVE ticket.

                    string? ticketId = await GetActiveTicketIdForChatId(chatId); //Efficient and Returns TicketId

                    if (ticketId != null)
                    {
                        TicketData? ticketData = await ticketService.GetTicketDataAsync(ticketId);  // Get TicketData
                        if (ticketData == null)
                        {
                            // This is a serious error. Log it and inform the user.
                            string errMessage = $"ERROR: TicketData is null for active ticketId {ticketId}.  ChatId: {chatId}.";
                            Console.WriteLine(errMessage);
                            if (_appConfig?.LogsChannelId != null)
                            {
                                await botClient.SendMessageSafeAsync(_appConfig.LogsChannelId, errMessage, cancellationToken: cancellationToken);
                            }
                            await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("errorOccurredMessage"), cancellationToken: cancellationToken);
                            return; // Stop processing.
                        }
                        if (ticketData.Status == "Closed")
                        {
                            // The ticket is closed, don't accept messages.
                            await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("closedTicketMessageUser"), cancellationToken: cancellationToken);
                            return;  // Stop processing.
                        }

                        // 1. Determine Message Type using MessageTypeHelper
                        string messageType = MessageTypeHelper.GetMessageType(message);
                        string? messageContent = MessageTypeHelper.GetMessageText(message);
                        string? mediaFileId = MessageTypeHelper.GetMediaFileId(message);

                        // 2. Create the ChatMessage object.
                        var userChatMessage = new ChatMessage(chatId.ToString(), "User", messageType) // Use chatId, not userId
                        {
                            TextContent = messageContent,
                            MediaFileId = mediaFileId,
                            Timestamp = DateTime.UtcNow
                        };

                        // 3. Append to Ticket History (ALWAYS, if ticket is open)
                        await ticketService.AppendMessageToTicketAsync(ticketId, userChatMessage);  // Simplified

                        // 4.  Check if Staff is Handling
                        var (isHandling, staffChatId) = ticketService.IsTicketBeingHandledAsync(ticketId); // Get handling status

                        // 5. Forward to staff if handling.
                        if (isHandling)
                        {
                            string userMessagePrefix = string.Format(_languageParser.GetMessage("userSentMessagePrefix"), ticketId, chatId); // Use language parser for prefix

                            try
                            {
                                Console.WriteLine($"Debug (UserBot): Forwarding User Message to Staff Bot Chat - Staff ChatId: {staffChatId}, Type: {staffChatId.GetType()}, User ChatId: {chatId}, TicketId: {ticketId}");

                                Stream? mediaStream = null; // Initialize mediaStream to null
                                string? caption = MessageTypeHelper.GetMessageText(message); // Get caption for media messages

                                if (!string.IsNullOrEmpty(mediaFileId) && messageType != "Text") // Download media only if FileId is present and it's not text
                                {
                                    try
                                    {
                                        Console.WriteLine($"Debug (UserBot): Requesting file with FileId: {mediaFileId} from User Message");

                                        // Gets the file from Telegram (using UserBot's botClient)
                                        var file = await botClient.GetFile(mediaFileId, cancellationToken);

                                        if (string.IsNullOrEmpty(file.FilePath))
                                        {
                                            Console.WriteLine("Error (UserBot): FilePath is null or empty for user media.");
                                            await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("errorOccurredMessage"), cancellationToken: cancellationToken); // Inform user of error
                                            return;
                                        }
                                        DotEnv.Load();
                                        // Constructs the correct file URL
                                        var botToken = Environment.GetEnvironmentVariable("USER_BOT_TOKEN"); // Use User Bot Token here!
                                        var fileUrl = $"https://api.telegram.org/file/bot{botToken}/{file.FilePath}";

                                        Console.WriteLine($"Debug (UserBot): Downloading file from {fileUrl} for Staff Chat");

                                        // Download the file using HttpClient
                                        mediaStream = new MemoryStream();
                                        using (HttpClient httpClient = new HttpClient())
                                        {
                                            var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                                            await mediaStream.WriteAsync(fileBytes, 0, fileBytes.Length, cancellationToken);
                                        }
                                        mediaStream.Position = 0; // Reset stream position
                                        Console.WriteLine($"Debug (UserBot): Media file downloaded successfully for FileId: {mediaFileId} for Staff Chat");
                                    }
                                    catch (Exception downloadEx)
                                    {
                                        Console.WriteLine($"Error (UserBot) downloading media file from Telegram API: {downloadEx}");
                                        mediaStream?.Dispose(); // Ensure stream is disposed in case of download error
                                        mediaStream = null; // Set to null to indicate download failure
                                        await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("errorOccurredMessage"), cancellationToken: cancellationToken); // Inform user of error
                                        return;
                                    }
                                }

                                // Now send the message to staff based on message type, using mediaStream if available
                                switch (messageType)
                                {
                                    case "Text":
                                        await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} {messageContent}", cancellationToken: cancellationToken) ?? Task.CompletedTask); // Send text
                                        break;
                                    case "Image":
                                        if (mediaStream != null)
                                        {
                                            InputFileStream inputFileStream = new InputFileStream(mediaStream, "user_image.jpg");
                                            await (Program.staffBot?.botClient?.SendPhotoSafeAsync(staffChatId, inputFileStream, caption: $"{userMessagePrefix} {caption}", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        else
                                        {
                                            await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} [Image - Error forwarding media]", cancellationToken: cancellationToken) ?? Task.CompletedTask); // Inform staff about media error
                                        }
                                        break;
                                    case "Audio":
                                        if (mediaStream != null)
                                        {
                                            InputFileStream inputFileStream = new InputFileStream(mediaStream, "user_audio.mp3"); // Adjust extension as needed
                                            await (Program.staffBot?.botClient?.SendAudio(staffChatId, inputFileStream, caption: $"{userMessagePrefix} {caption}", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        else
                                        {
                                            await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} [Audio - Error forwarding media]", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        break;
                                    case "Voice":
                                        if (mediaStream != null)
                                        {
                                            InputFileStream inputFileStream = new InputFileStream(mediaStream, "user_voice.ogg"); // Use .ogg for voice
                                            await (Program.staffBot?.botClient?.SendVoice(staffChatId, inputFileStream, caption: $"{userMessagePrefix} {caption}", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        else
                                        {
                                            await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} [Voice Message - Error forwarding media]", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        break;
                                    case "Video":
                                        if (mediaStream != null)
                                        {
                                            InputFileStream inputFileStream = new InputFileStream(mediaStream, "user_video.mp4"); // Use .mp4 for video
                                            await (Program.staffBot?.botClient?.SendVideo(staffChatId, inputFileStream, caption: $"{userMessagePrefix} {caption}", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        else
                                        {
                                            await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} [Video - Error forwarding media]", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        break;
                                    case "Document":
                                        if (mediaStream != null)
                                        {
                                            InputFileStream inputFileStream = new InputFileStream(mediaStream, "user_document.zip"); // Adjust extension
                                            await (Program.staffBot?.botClient?.SendDocument(staffChatId, inputFileStream, caption: $"{userMessagePrefix} {caption}", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        else
                                        {
                                            await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} [Document - Error forwarding media]", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        }
                                        break;
                                    default:
                                        await (Program.staffBot?.botClient?.SendMessageSafeAsync(staffChatId, $"{userMessagePrefix} [Unsupported Media Type - Error forwarding]", cancellationToken: cancellationToken) ?? Task.CompletedTask);
                                        break;
                                }


                                await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("staffRepliedToYourTicketMessage"), cancellationToken: cancellationToken);
                            }
                            catch (Exception forwardEx)
                            {
                                Console.WriteLine($"Error forwarding message to staff chat: {forwardEx}");
                                await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("errorOccurredMessage"), cancellationToken: cancellationToken); // Inform user of error
                                                                                                                                                                        // Optionally log error to logs channel as well
                            }
                        }

                    }

                }
                else if (text != null && (text.ToLower() == "/start" || text.ToLower() == "/menu"))
                {
                    var replyKeyboardMarkup = new ReplyKeyboardMarkup(
                        new[]
                        {
                            new KeyboardButton[] { _languageParser.GetMessage("contactSupportButton") },
                            new KeyboardButton[] { _languageParser.GetMessage("menuButton"), _languageParser.GetMessage("languageButton") }
                        })
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = false // Keep it persistent
                    };

                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("welcomeMessage"), replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
                    return;
                }
                else if (text == _languageParser.GetMessage("menuButton"))
                {
                    var menuKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("contactSupportButton"), "consult_staff"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("socialsMessage"), "socials"),
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("servicesMessage"), "check_services")
                }
                    });

                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("menuMessage"), replyMarkup: menuKeyboard, cancellationToken: cancellationToken);
                    return;
                }

                else if (text == _languageParser.GetMessage("contactSupportButton"))
                {
                    await StartTicketAsync(chatId, null, cancellationToken); // Pass null for initial message
                    return;
                }
                else if (text == _languageParser.GetMessage("languageButton"))
                {
                    Console.WriteLine("Debug: Language button pressed - Generating language selection keyboard...");

                    var languageKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("englishButton"), "set_language_en") }, // Row 1: English Button
                        new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("amharicButton"), "set_language_am") }  // Row 2: Amharic Button
                    });

                    Console.WriteLine($"Debug: Language keyboard created - Rows: {languageKeyboard.InlineKeyboard.Count()}, Buttons per row 1: {languageKeyboard.InlineKeyboard.ToList()[0].Count()}, Buttons per row 2: {languageKeyboard.InlineKeyboard.ToList()[1].Count()}"); // Log keyboard structure
                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("selectLanguageMessage"), replyMarkup: languageKeyboard, cancellationToken: cancellationToken);

                    Console.WriteLine("Debug: Language selection message sent."); // Log message sent
                    return;
                }
                else
                {
                    // User doesn't have an active ticket and didn't send a start command.
                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("messagingOutsideActiveTicket"), cancellationToken: cancellationToken);
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in HandleMessageAsync (UserBot): {apiEx.Message}");
                string errorMessage = $"Telegram API Error in UserBot: {apiEx.Message}. ChatId: {chatId}.";
                await (Program.staffBot?.botClient?.SendMessageSafeAsync(_appConfig?.LogsChannelId ?? 0, errorMessage, cancellationToken: cancellationToken) ?? Task.CompletedTask); // Send to logs channel
                await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("errorOccurredMessage"), cancellationToken: cancellationToken); // Use language parser
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error in HandleMessageAsync (UserBot): {ex}");
                string errorMessage = $"General Error in UserBot: {ex.ToString()}. ChatId: {chatId}.";
                await (Program.staffBot?.botClient?.SendMessageSafeAsync(_appConfig?.LogsChannelId ?? 0, errorMessage, cancellationToken: cancellationToken) ?? Task.CompletedTask);
                await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("errorOccurredMessage"), cancellationToken: cancellationToken); // Use language parser
            }
        }

        private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, long userId, CancellationToken cancellationToken) // Added userId
        {
            // Check if the language parser is initialized.
            if (_languageParser == null)
            {
                Console.WriteLine("Error: LanguageParser is not initialized in UserBot (HandleCallbackQueryAsync).");
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ An internal error occurred. Please try again.", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            string callbackData = callbackQuery.Data ?? string.Empty;
            long chatId = callbackQuery.Message?.Chat?.Id ?? 0;

            try
            {
                switch (callbackData)
                {
                    case "consult_staff":
                        await StartTicketAsync(chatId, null, cancellationToken);
                        break;
                    case "check_services":
                        await CheckServicesAsync(chatId, cancellationToken);
                        break;
                    case "socials":
                        await SendSocialsAsync(chatId, cancellationToken);
                        break;
                    case "close_ticket":
                        await CloseTicketByUserAsync(chatId, cancellationToken);
                        break;
                    case "set_language_en":  
                    await ticketService.SetUserLanguageAsync(userId, "en");
                        _languageParser = new LanguageParser("en"); 
                        await ShowMainMenuAsync(chatId, cancellationToken); 
                        break;
                    case "set_language_am": 
                        await ticketService.SetUserLanguageAsync(userId, "am");
                        _languageParser = new LanguageParser("am"); 
                        await ShowMainMenuAsync(chatId, cancellationToken); 
                        break;
                    default:
                        if (!string.IsNullOrEmpty(callbackData))
                            await botClient.AnswerCallbackQuery(callbackQuery.Id, _languageParser.GetMessage("invalidSelectionMessage"), showAlert: true, cancellationToken: cancellationToken); // Use language parser
                        break;
                }
                if (!string.IsNullOrEmpty(callbackData))  // Use !string.IsNullOrEmpty
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, $"You selected: {callbackData}", cancellationToken: cancellationToken);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleCallbackQueryAsync (UserBot): {ex}");
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ An error occurred while processing your request. Please try again.", showAlert: true, cancellationToken: cancellationToken);
            }
        }

        private async Task ShowMainMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            if (_languageParser == null)
            {
                Console.WriteLine("Error: LanguageParser is not initialized in UserBot (ShowMainMenuAsync).");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ An internal error occurred. Please try again later.", cancellationToken: cancellationToken);
                return;
            }
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(
                new[]
                {
                        new KeyboardButton[] { _languageParser.GetMessage("contactSupportButton") },
                        new KeyboardButton[] { _languageParser.GetMessage("menuButton"), _languageParser.GetMessage("languageButton") }
                    })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false 
            };

            await botClient.SendMessage(chatId, _languageParser.GetMessage("welcomeMessage"), replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        private async Task StartTicketAsync(long chatId, string? initialMessageText, CancellationToken cancellationToken)
        {
            string createTicketResult = await ticketService.CreateTicketAsync(chatId, initialMessageText ?? "");

            string? ticketId = null;
            var activeTickets = await ticketService.GetAllTicketsAsync(); //don't use this use active tickets
            var userTicket = activeTickets.FirstOrDefault(t => t.ChatId == chatId);
            if (userTicket != null)
            {
                ticketId = userTicket.TicketId;
            }

            try
            {
                string logMessage = $"📩 New ticket created - Ticket ID: {ticketId}, User ID: {chatId}.";

                if (_appConfig?.LogsChannelId is long logsChannelId) // Use the correct field
                {
                    await botClient.SendMessageSafeAsync(logsChannelId, logMessage, cancellationToken: cancellationToken);
                }
                else
                {
                    Console.WriteLine("Error: LogsChannelId is not properly configured or is missing when trying to send log message.");
                }
                if(_languageParser != null)
                {
                    await botClient.SendMessageSafeAsync(chatId, string.Format(_languageParser.GetMessage("ticketCreatedMessage"), ticketId), cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, $"Ticket created successfully. Your ticket ID is: {ticketId}", cancellationToken: cancellationToken);
                }
                
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error starting ticket (UserBot): {apiEx.Message}");
                if (_languageParser != null)
                {
                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("failedToCreateTicketMessage"), cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, "Failed to create ticket. Please try again later.", cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StartTicketAsync (UserBot): {ex}");
                if (_languageParser != null)
                {
                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("failedToCreateTicketMessage"), cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, "Failed to create ticket. Please try again later.", cancellationToken: cancellationToken);
                }
            }
        }

        private async Task CloseTicketByUserAsync(long chatId, CancellationToken cancellationToken)
        {

            string closeTicketResult = await ticketService.CloseTicketByChatIdAsync(chatId);

            try
            {
                await botClient.SendMessageSafeAsync(chatId, closeTicketResult, cancellationToken: cancellationToken);
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error closing ticket by user (UserBot): {apiEx.Message}");
                if(_languageParser != null)
                {
                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("failedToCloseTicketMessage"), cancellationToken: cancellationToken); // Use language parser
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, "Failed to close ticket. Please try again later.", cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {   // Use specific config property
                Console.WriteLine($"Error in CloseTicketByUserAsync (UserBot): {ex}");
                if(_languageParser != null)
                {
                    await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("failedToCloseTicketMessage"), cancellationToken: cancellationToken); // Use language Peraser
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, "Failed to close ticket. Please try again later.", cancellationToken: cancellationToken);
                }
            }
        }
        private async Task CheckServicesAsync(long chatId, CancellationToken cancellationToken)
        {

            if (_languageParser == null)
            {
                Console.WriteLine("Error: LanguageParser is not initialized in UserBot (CheckServicesAsync).");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ An internal error occurred. Please try again later.", cancellationToken: cancellationToken);
                return;
            }
            string servicesMessage = _languageParser.GetMessage("servicesMessage");

            try
            {
                await botClient.SendMessageSafeAsync(chatId, servicesMessage, cancellationToken: cancellationToken);
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error checking services (UserBot): {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Could not retrieve service information. Please try again later.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckServicesAsync (UserBot): {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Failed to check services. Please try again later.", cancellationToken: cancellationToken);
            }
        }

        private async Task SendSocialsAsync(long chatId, CancellationToken cancellationToken)
        {

            if (_languageParser == null)
            {
                Console.WriteLine("Error: LanguageParser is not initialized in UserBot (SendSocialsAsync).");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ An internal error occurred. Please try again later.", cancellationToken: cancellationToken);
                return;
            }
            string socialsMessage = _languageParser.GetMessage("socialsMessage");
            try
            {
                await botClient.SendMessageSafeAsync(chatId, socialsMessage, cancellationToken: cancellationToken);
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error sending socials (UserBot): {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Could not retrieve social media links. Please try again later.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendSocialsAsync (UserBot): {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Failed to send social media links. Please try again later.", cancellationToken: cancellationToken);
            }
        }

        // UserBot.cs

        public async Task ReceiveStaffReplyAsync(long userChatId, Message staffReplyMessage, string messageType, Stream? fileStream, string? replyText, string staffName, CancellationToken cancellationToken) // Modified signature to accept Stream? fileStream
        {
            try
            {
                string formattedStaffReply = _languageParser != null ? $"{_languageParser.GetMessage("staffReplied")}\n\n" : "Staff replied:\n\n";
                string? text = staffReplyMessage.Text; // Default to text from staffReplyMessage

                if (messageType != "Text" && !string.IsNullOrEmpty(staffReplyMessage.Caption))
                {
                    formattedStaffReply += staffReplyMessage.Caption; // Use caption for media messages if available
                    text = staffReplyMessage.Caption; // Also set 'text' to caption for media messages
                }

                switch (messageType)
                {
                    case "Text":
                        Console.WriteLine($"Debug (UserBot): Sending Text Reply - User ChatId: {userChatId}"); // ADDED LOG
                        await botClient.SendMessageSafeAsync(userChatId, $"{formattedStaffReply}{text}", ParseMode.Html, cancellationToken: cancellationToken);
                        break;
                    case "Image":
                        Console.WriteLine($"Debug (UserBot): Sending Image Reply to User - User ChatId: {userChatId}, InputFile Type: InputFileStream"); // Updated log
                        if (fileStream != null) // Check if stream is not null
                        {
                            InputFileStream inputFileStream = new InputFileStream(fileStream, "staff_reply_image.jpg"); // You can set a filename here
                            await botClient.SendPhotoSafeAsync(userChatId, photo: inputFileStream, caption: formattedStaffReply, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessageSafeAsync(userChatId, "[Image - Error: Missing File Stream]", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        break;
                    case "Audio":
                        Console.WriteLine($"Debug (UserBot): Sending Audio Reply to User - User ChatId: {userChatId}, InputFile Type: InputFileStream"); // Updated log
                        if (fileStream != null) // Check if stream is not null
                        {
                            InputFileStream inputFileStream = new InputFileStream(fileStream, "staff_reply_audio.mp3"); // You can set a filename here - adjust extension
                            await botClient.SendAudio(userChatId, audio: inputFileStream, caption: formattedStaffReply, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessageSafeAsync(userChatId, "[Audio - Error: Missing File Stream]", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        break;
                    case "Voice":
                        Console.WriteLine($"Debug (UserBot): Sending Voice Reply to User - User ChatId: {userChatId}, InputFile Type: InputFileStream"); // Updated log
                        if (fileStream != null) // Check if stream is not null
                        {
                            InputFileStream inputFileStream = new InputFileStream(fileStream, "staff_reply_voice.ogg"); // You can set a filename here - use .ogg for voice
                            await botClient.SendVoice(userChatId, voice: inputFileStream, caption: formattedStaffReply, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessageSafeAsync(userChatId, "[Voice - Error: Missing File Stream]", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        break;
                    case "Video":
                        Console.WriteLine($"Debug (UserBot): Sending Video Reply to User - User ChatId: {userChatId}, InputFile Type: InputFileStream"); // Updated log
                        if (fileStream != null) // Check if stream is not null
                        {
                            InputFileStream inputFileStream = new InputFileStream(fileStream, "staff_reply_video.mp4"); // You can set a filename here - use .mp4 for video
                            await botClient.SendVideo(userChatId, video: inputFileStream, caption: formattedStaffReply, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessageSafeAsync(userChatId, "[Video - Error: Missing File Stream]", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        break;
                    case "Document":
                        Console.WriteLine($"Debug (UserBot): Sending Document Reply to User - User ChatId: {userChatId}, InputFile Type: InputFileStream"); // Updated log
                        if (fileStream != null) // Check if stream is not null
                        {
                            InputFileStream inputFileStream = new InputFileStream(fileStream, "staff_reply_document.zip"); // You can set a filename here - adjust extension accordingly
                            await botClient.SendDocument(userChatId, document: inputFileStream, caption: formattedStaffReply, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessageSafeAsync(userChatId, "[Document - Error: Missing File Stream]", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        break;
                    default:
                        if (_languageParser != null)
                        {
                            await botClient.SendMessageSafeAsync(userChatId, _languageParser.GetMessage("unsupportedMessageTypeReceivedFromStaffMessage"), cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessageSafeAsync(userChatId, "Unsupported message type received from staff.", cancellationToken: cancellationToken);
                        }
                        break;
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in ReceiveStaffReplyAsync (UserBot): {apiEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReceiveStaffReplyAsync (UserBot): {ex}");
            }
        }

        private async Task DisplayMessageHistory(long chatId, string ticketId, CancellationToken cancellationToken)
        {
            try
            {
                List<ChatMessage> messageHistory = await ticketService.GetTicketMessageHistoryAsync(ticketId);

                if (messageHistory.Count > 0)
                {
                    if (_languageParser == null)
                    {
                        Console.WriteLine("Error: LanguageParser is not initialized in DisplayMessageHistory.");
                        await botClient.SendMessageSafeAsync(chatId, "⚠️ An internal error occurred. Please try again later.", cancellationToken: cancellationToken);
                        return;
                    }
                    string historyText = _languageParser.GetMessage("messageHistory");
                    foreach (ChatMessage chatMessage in messageHistory)
                    {
                        string messageContentDisplay = "";

                        if (chatMessage.MessageType == "Text" && !string.IsNullOrEmpty(chatMessage.TextContent))
                        {
                            messageContentDisplay = $"<code>{chatMessage.SenderType}: {chatMessage.TextContent}</code>\n";
                        }
                        else if (chatMessage.MessageType == "Image")
                        {
                            messageContentDisplay = $"<code>{chatMessage.SenderType}: [Image]</code>\n"; // Simplified
                        }
                        else if (chatMessage.MessageType == "Audio")
                        {
                            messageContentDisplay = $"<code>{chatMessage.SenderType}: [Audio]</code>\n";
                        }
                        else if (chatMessage.MessageType == "Voice")
                        {
                            messageContentDisplay = $"<code>{chatMessage.SenderType}: [Voice Message]</code>\n";
                        }
                        else if (chatMessage.MessageType == "Video")
                        {
                            messageContentDisplay = $"<code>{chatMessage.SenderType}: [Video]</code>\n";
                        }
                        else if (chatMessage.MessageType == "Document")
                        {
                            messageContentDisplay = $"<code>{chatMessage.SenderType}: [Document]</code>\n";
                        }
                        historyText += messageContentDisplay;
                    }


                    await botClient.SendMessageSafeAsync(chatId, historyText, ParseMode.Html, cancellationToken: cancellationToken);
                }
                else
                {
                    // Handle the case where there is no message history
                    if (_languageParser != null)
                    {
                        await botClient.SendMessageSafeAsync(chatId, _languageParser.GetMessage("noMessagesYet"), ParseMode.Html, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        Console.WriteLine("Error: LanguageParser is not initialized in DisplayMessageHistory.");
                        await botClient.SendMessageSafeAsync(chatId, "⚠️ An internal error occurred. Please try again later.", cancellationToken: cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying message history: {ex}");
            }
        }

    }
}