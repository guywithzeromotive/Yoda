using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Concurrent;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yoda_Bot.Services;
using Yoda_Bot.Config;
using Yoda_Bot.Models;
using Yoda_Bot.Utils;
using Yoda_Bot.Helpers;
using dotenv.net;

namespace Yoda_Bot.TelegramBots
{
    public class StaffBot
    {
        public readonly ITelegramBotClient botClient;
        private readonly TicketService ticketService;
        private readonly long logsChannelId;
        private readonly List<long> adminUserIds;
        private readonly UserBot userBotInstance;
        private readonly ConcurrentDictionary<long, string> _handlingTicketForStaff = new ConcurrentDictionary<long, string>();
        private LanguageParser _languageParser;
        private readonly ConcurrentDictionary<string, List<TicketData>> _activeTicketListCache = new ConcurrentDictionary<string, List<TicketData>>();
        private static readonly TimeSpan CacheExpirationTime = TimeSpan.FromMinutes(5);
        private DateTime _lastCacheUpdateTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, TicketData> _ticketDetailsCache = new ConcurrentDictionary<string, TicketData>();
        private static readonly TimeSpan TicketDetailsCacheExpirationTime = TimeSpan.FromMinutes(10);
        private readonly Dictionary<string, DateTime> _ticketDetailsCacheExpiration = new Dictionary<string, DateTime>();
        private readonly AppConfig _appConfig;

        public StaffBot(ITelegramBotClient botClient, TicketService ticketService, AppConfig appConfig, List<long> adminUserIds, UserBot userBotInstance) // Use AppConfig, removed EnvConfig
        {
            this.botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
            this.ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
            this._appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig), "AppConfig is not properly configured.");
            this.logsChannelId = appConfig.LogsChannelId; // get logsChannelId from AppConfig
            this.adminUserIds = adminUserIds ?? throw new ArgumentNullException(nameof(adminUserIds));
            this.userBotInstance = userBotInstance ?? throw new ArgumentNullException(nameof(userBotInstance), "UserBot instance cannot be null.");
            _languageParser = new LanguageParser("en");
            SetUpMenuButton().Wait();
        }

        private async Task SetUpMenuButton()
        {
            try
            {
                await botClient.SetMyCommands(new List<BotCommand>()
                {
                    new BotCommand() { Command = "start", Description = "Show the main menu" },
                    new BotCommand() { Command = "menu", Description = "Show the main menu" }
                }, new BotCommandScopeDefault(), cancellationToken: default);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up menu button: {ex}");
            }
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is { } message)
                {
                    await HandleMessageAsync(message, cancellationToken);
                }
                else if (update.CallbackQuery is { } callbackQuery)
                {
                    await HandleCallbackQueryAsync(callbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StaffBot.HandleUpdateAsync: {ex}");
                // Consider re-throwing the exception or handling it further.
            }
        }

        private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
        {
            string text = message.Text ?? "";
            long staffChatId = message.Chat.Id;

            if (IsAdmin(staffChatId))
            {
                if (text.ToLower().StartsWith("/admin"))
                {
                    await HandleAdminCommandAsync(message, cancellationToken);
                    return;
                }
            }

            if (_handlingTicketForStaff.TryGetValue(staffChatId, out string? handledTicketId))
            {
                await ProcessContinuousStaffReplyAsync(message, handledTicketId, cancellationToken);
                return;
            }

            if (IsTicketIdFormat(text))
            {
                string ticketIdToSearch = text.Trim().ToUpperInvariant();
                await SearchTicketAsync(staffChatId, ticketIdToSearch, cancellationToken);
                return;
            }

            if (text.ToLower().StartsWith("/delete"))
            {
                string[] parts = text.Split(' ');
                if (parts.Length == 2 && IsTicketIdFormat(parts[1]))
                {
                    string ticketIdToDelete = parts[1].ToUpperInvariant();
                    await SendDeleteTicketConfirmationAsync(staffChatId, ticketIdToDelete, null, cancellationToken);
                    return;
                }
                await botClient.SendMessageSafeAsync(staffChatId, _languageParser.GetMessage("usageDeleteTicketMessage"), cancellationToken: cancellationToken); // Use language parser
                return;
            }


            if (text.ToLower() == "/start" || text.ToLower() == "/menu")
            {
                var replyKeyboardMarkup = new ReplyKeyboardMarkup(
                    new[]
                    {
                        new KeyboardButton[] { "🎫 View Tickets" },
                        new KeyboardButton[] { "🔍 Search Tickets" },
                        new KeyboardButton[] { "📊 Ticket Stats" }
                    })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = false
                };
                // Use language parser
                await botClient.SendMessage(staffChatId, _languageParser.GetMessage("staffMenuMessage"), replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
                return;
            }

            if (text == "🎫 View Tickets")
            {
                ClearHandlingState(staffChatId);
                await SendViewTicketsOptionsAsync(staffChatId, cancellationToken);
                return;
            }
            if (text == "🔍 Search Tickets")
            {
                await InitiateTicketSearchAsync(staffChatId, cancellationToken);
                return;
            }
            if (text.ToLower() == "📊 ticket stats")
            {
                // TODO: Implement ticket stats.
                return;
            }
        }

        private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            string callbackData = callbackQuery.Data ?? string.Empty;
            long chatId = callbackQuery.Message?.Chat?.Id ?? 0;
            int? messageIdToEdit = callbackQuery.Message?.MessageId;

            try
            {
                Console.WriteLine($"Debug: HandleCallbackQueryAsync - Received callbackData: '{callbackData}'");

                if (callbackData.StartsWith("staff_close_ticket-"))
                {
                    string ticketIdToClose = callbackData.Substring("staff_close_ticket-".Length);
                    await CloseTicketByStaffAsync(ticketIdToClose, cancellationToken);
                }
                else if (callbackData == "view_tickets")
                {
                    ClearHandlingState(chatId);
                    await SendViewTicketsOptionsAsync(chatId, cancellationToken);
                }
                else if (callbackData.StartsWith("view_tickets_type-"))
                {
                    ClearHandlingState(chatId);
                    string[] parts = callbackData.Substring("view_tickets_type-".Length).Split("-page-");
                    string ticketType = parts[0];
                    int pageNumber = 1;

                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedPageNumber))
                    {
                        pageNumber = parsedPageNumber;
                    }

                    switch (ticketType)
                    {
                        case "open":
                            await ViewTicketsAsync(chatId, pageNumber, 10, messageIdToEdit, null, "open", cancellationToken);
                            break;
                        case "closed":
                            await ViewTicketsAsync(chatId, pageNumber, 10, messageIdToEdit, null, "closed", cancellationToken);
                            break;
                        case "all":
                            await ViewTicketsAsync(chatId, pageNumber, 10, messageIdToEdit, null, "all", cancellationToken);
                            break;
                        default:
                            await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Invalid ticket type.", showAlert: true, cancellationToken: cancellationToken);
                            Console.WriteLine($"ERROR: Invalid ticket type in callback: {callbackData}");
                            return;
                    }
                }

                else if (callbackData.StartsWith("handle_ticket-"))
                {
                    /* if (int.TryParse(callbackData.Substring("handle_ticket-".Length), out int selectedTicketNumber))
                     {
                         await HandleTicketSelectionAsync(chatId, selectedTicketNumber, cancellationToken);
                     }
                     else
                     {
                         await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Invalid ticket number selected.", showAlert: true, cancellationToken: cancellationToken);
                     }*/
                    string ticketIdToHandle = callbackData.Substring("handle_ticket_by_id-".Length);
                    Console.WriteLine($"Debug (StaffBot): Handle Ticket Callback - TicketId: {ticketIdToHandle}, Staff ChatId: {chatId}");
                    await HandleTicketSelectionAsync(chatId, ticketIdToHandle, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_confirm_close_ticket-")) // STEP 1: Close Confirmation
                {
                    string ticketIdConfirmation = callbackData.Substring("staff_confirm_close_ticket-".Length);
                    await SendCloseTicketConfirmationAsync(chatId, ticketIdConfirmation, messageIdToEdit, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_do_close_ticket-")) // STEP 2:  Actually CLOSE the ticket.
                {
                    string ticketIdToClose = callbackData.Substring("staff_do_close_ticket-".Length);
                    await PerformTicketClosureAsync(chatId, ticketIdToClose, messageIdToEdit, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_confirm_delete_ticket-")) // STEP 1: Delete Confirmation
                {
                    string ticketIdConfirmation = callbackData.Substring("staff_confirm_delete_ticket-".Length);
                    await SendDeleteTicketConfirmationAsync(chatId, ticketIdConfirmation, messageIdToEdit, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_do_delete_with_transcript-")) // STEP 2: Delete (with transcript)
                {
                    string ticketIdToDeleteTranscript = callbackData.Substring("staff_do_delete_with_transcript-".Length);
                    await PerformTranscriptAndTicketDeletionAsync(chatId, ticketIdToDeleteTranscript, messageIdToEdit, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_delete_only-"))//STEP 2: Delete Only
                {
                    string ticketIdToDelete = callbackData.Substring("staff_delete_only-".Length);
                    await PerformTicketDeletionAsync(chatId, ticketIdToDelete, messageIdToEdit, cancellationToken); // The existing deletion method

                }
                else if (callbackData.StartsWith("staff_cancel_close_ticket-")) // CANCEL (from either Close or Delete)
                {
                    string ticketIdToReopen = callbackData.Substring("staff_cancel_close_ticket-".Length);
                    await RevertToTicketDetailsViewAsync(chatId, ticketIdToReopen, messageIdToEdit, cancellationToken);
                }

                else if (callbackData.StartsWith("staff_get_transcript-"))
                {
                    string ticketIdTranscript = callbackData.Substring("staff_get_transcript-".Length);
                    await SendTicketTranscriptToLogsAsync(chatId, ticketIdTranscript, messageIdToEdit, cancellationToken);
                }

                else if (callbackData.StartsWith("staff_reopen_ticket-"))
                {
                    string ticketIdToReopen = callbackData.Substring("staff_reopen_ticket-".Length);
                    await SendReopenTicketConfirmationAsync(chatId, ticketIdToReopen, messageIdToEdit, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_do_reopen_ticket-"))
                {
                    string ticketIdToReopen = callbackData.Substring("staff_do_reopen_ticket-".Length);
                    await PerformTicketReopeningAsync(chatId, ticketIdToReopen, messageIdToEdit, cancellationToken);
                }

                else if (callbackData.StartsWith("reply_to_user_by_id-"))
                {
                    string ticketIdForReply = callbackData.Substring("reply_to_user_by_id-".Length);
                    await InitiateReplyToUserByTicketIdAsync(chatId, ticketIdForReply, cancellationToken);
                }
                else if (callbackData.StartsWith("handle_ticket_by_id-"))
                {
                    string ticketIdToHandle = callbackData.Substring("handle_ticket_by_id-".Length);
                    await HandleTicketSelectionByTicketIdAsync(chatId, ticketIdToHandle, cancellationToken);
                }
                else if (callbackData.StartsWith("staff_switch_ticket-"))
                {
                    string ticketId = callbackData.Substring("staff_switch_ticket-".Length);
                    // Retrieve the current message ID SAFELY
                    if (callbackQuery.Message != null) // NULL CHECK HERE
                    {
                        int messageId = callbackQuery.Message.MessageId;
                        await PerformTicketSwitchAsync(chatId, ticketId, messageId, cancellationToken);
                    }
                    else
                    {
                        // Handle the case where callbackQuery.Message is null.  You might:
                        await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Cannot switch ticket: original message not found.", showAlert: true, cancellationToken: cancellationToken);
                        Console.WriteLine("Error: callbackQuery.Message is NULL in staff_switch_ticket- callback.");
                    }

                    return; // Important!  We've handled this case.
                }
                else if (callbackData == "search_tickets")
                {
                    await InitiateTicketSearchAsync(chatId, cancellationToken);
                }
                else
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Invalid selection!", showAlert: true, cancellationToken: cancellationToken);
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, $"You selected: {callbackData}", cancellationToken: cancellationToken);

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in StaffBot.HandleCallbackQueryAsync: {apiEx.Message}");
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ An error occurred while processing your request. Please try again.", showAlert: true, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error in StaffBot.HandleCallbackQueryAsync: {ex}");
                Console.WriteLine($"Error Details: {ex}");
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ An unexpected error occurred. Please try again.", showAlert: true, cancellationToken: cancellationToken);
            }
        }
        private async Task HandleTicketSelectionAsync(long chatId, string ticketIdToHandle , CancellationToken cancellationToken)//int selectedTicketNumber
        {
            try
            {
                /*List<TicketData> allOpenTickets = await ticketService.GetOpenTicketsAsync(); // Get open tickets (for ticket number mapping)
                var ticketNumberMap = new Dictionary<int, string>();

                int startIndex = 0;
                for (int i = 0; i < allOpenTickets.Count; i++)
                {
                    int ticketNumber = startIndex + i + 1;
                    ticketNumberMap[ticketNumber] = allOpenTickets[i].TicketId ?? "UnknownTicketId";
                }

                if (ticketNumberMap.TryGetValue(selectedTicketNumber, out string? selectedTicketId))
                {
                    await ShowTicketDetailsAsync(chatId, selectedTicketId, cancellationToken, messagePrefix: $"<b>Ticket Number:</b> <code>{selectedTicketNumber}</code>\n"); // Call ShowTicketDetailsAsync with prefix
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, $"⚠️ Invalid ticket number selected: {selectedTicketNumber}", cancellationToken: cancellationToken);
                }*/
                await ShowTicketDetailsAsync(chatId, ticketIdToHandle, cancellationToken); // Call ShowTicketDetailsAsync directly

                _handlingTicketForStaff.AddOrUpdate(chatId, ticketIdToHandle, (key, oldValue) => ticketIdToHandle);
                Console.WriteLine($"Debug (StaffBot): _handlingTicketForStaff Updated - Staff ChatId: {chatId}, TicketId: {ticketIdToHandle}"); // Log AFTER update
                ticketService.ActiveTicketsChatIdToTicketIdMap.TryAdd(chatId, ticketIdToHandle); // Add to ActiveTicketsChatIdToTicketIdMap as well
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in HandleTicketSelectionAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error retrieving ticket details.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleTicketSelectionAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error retrieving ticket details. Please try again.", cancellationToken: cancellationToken);
            }
        }
        private async Task InitiateReplyToUserByTicketIdAsync(long staffChatIdForReply, string ticketIdForReply, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: InitiateReplyToUserByTicketIdAsync - Start. chatId: {staffChatIdForReply}, ticketIdForReply: {ticketIdForReply}"); // Log start + parameters

                TicketData? selectedTicketData = await ticketService.GetTicketDataAsync(ticketIdForReply);

                if (selectedTicketData != null)
                {
                    _handlingTicketForStaff.AddOrUpdate(staffChatIdForReply, ticketIdForReply, (key, oldValue) => ticketIdForReply);

                    // Add to activeTicketsChatIdToTicketIdMap to mark as "active"
                    ticketService.ActiveTicketsChatIdToTicketIdMap.TryAdd(selectedTicketData.ChatId, ticketIdForReply); // KEY CHANGE

                    await botClient.SendMessageSafeAsync(staffChatIdForReply, string.Format(_languageParser.GetMessage("youAreNowHandlingTicketMessage"), ticketIdForReply), ParseMode.Html, cancellationToken: cancellationToken);

                    await botClient.SendMessageSafeAsync(
                        staffChatIdForReply,
                        string.Format(_languageParser.GetMessage("replyToUserByTicketIdMessage"), ticketIdForReply, selectedTicketData.ChatId),
                        ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await botClient.SendMessageSafeAsync(staffChatIdForReply, string.Format(_languageParser.GetMessage("errorCouldNotFindTicketDataForReplyMessage"), ticketIdForReply), cancellationToken: cancellationToken);
                }
                Console.WriteLine($"StaffBot: InitiateReplyToUserByTicketIdAsync - End. chatId: {staffChatIdForReply}, ticketIdForReply: {ticketIdForReply}"); // Log end + parameters

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in InitiateReplyToUserByTicketIdAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(staffChatIdForReply, _languageParser.GetMessage("telegramApiErrorInitiatingReplyMessage"), cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitiateReplyToUserByTicketIdAsync: {ex}");
                await botClient.SendMessageSafeAsync(staffChatIdForReply, _languageParser.GetMessage("errorInitiatingReplyToUserMessage"), cancellationToken: cancellationToken);
            }
        }
        private async Task ProcessStaffReplyAsync(Message staffReplyMessage, CancellationToken cancellationToken)
        {
            try
            {
                string? replyText = staffReplyMessage.Text;  // Text content (for text messages)
                string messageType = "Text"; // Default
                string? mediaFileId = null;

                // --- Handle different message types from Staff ---
                if (staffReplyMessage.Photo != null && staffReplyMessage.Photo.Length > 0)
                {
                    messageType = "Image";
                    mediaFileId = staffReplyMessage.Photo.LastOrDefault()?.FileId;
                    replyText = staffReplyMessage.Caption;  //Use caption text
                }
                else if (staffReplyMessage.Audio != null)
                {
                    messageType = "Audio";
                    mediaFileId = staffReplyMessage.Audio.FileId;
                    replyText = staffReplyMessage.Caption;
                }
                else if (staffReplyMessage.Voice != null)
                {
                    messageType = "Voice";
                    mediaFileId = staffReplyMessage.Voice.FileId;
                    replyText = staffReplyMessage.Caption;
                }
                else if (staffReplyMessage.Video != null)
                {
                    messageType = "Video";
                    mediaFileId = staffReplyMessage.Video.FileId;
                    replyText = staffReplyMessage.Caption;
                }
                else if (staffReplyMessage.Document != null) // Handle Documents
                {
                    messageType = "Document";
                    mediaFileId = staffReplyMessage.Document.FileId;
                    replyText = staffReplyMessage.Caption; // Documents can have captions too
                }

                // --- End Handle Message Types ---
                if (string.IsNullOrEmpty(replyText) && string.IsNullOrEmpty(mediaFileId)) //Check for both text and media
                {
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, "⚠️ Staff reply message was empty. Please try again.", cancellationToken: cancellationToken);
                    return;
                }

                string? replyToMessageText = staffReplyMessage.ReplyToMessage?.Text;
                if (replyToMessageText == null || !replyToMessageText.StartsWith("💬 Reply to User - Ticket #"))
                {
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, "⚠️ Invalid reply context. Please reply to the 'Reply to User' message.", cancellationToken: cancellationToken);
                    return;
                }

                string ticketNumberStr = replyToMessageText.Substring("💬 Reply to User - Ticket #".Length).Split('\n')[0].Trim();
                if (!int.TryParse(ticketNumberStr, out int repliedTicketNumber))
                {
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, "⚠️ Could not parse ticket number from reply context.", cancellationToken: cancellationToken);
                    return;
                }

                List<TicketData> allOpenTickets = await ticketService.GetOpenTicketsAsync();
                var ticketNumberMap = new Dictionary<int, string>();
                int startIndex = 0;
                for (int i = 0; i < allOpenTickets.Count; i++)
                {
                    int ticketNumber = startIndex + i + 1;
                    ticketNumberMap[ticketNumber] = allOpenTickets[i].TicketId ?? "UnknownTicketId";
                }


                if (ticketNumberMap.TryGetValue(repliedTicketNumber, out string? repliedTicketId))
                {
                    TicketData? repliedTicketData = await ticketService.GetTicketDataAsync(repliedTicketId);
                    if (repliedTicketData != null)
                    {
                        long userChatId = repliedTicketData.ChatId;

                        // Get staff name safely:
                        string staffName = (staffReplyMessage.From?.FirstName ?? "") + " " + (staffReplyMessage.From?.LastName ?? "");
                        staffName = string.IsNullOrWhiteSpace(staffName) ? "Staff" : staffName.Trim();
                        Console.WriteLine($"Debug (StaffBot): Sending Reply to User - Message Type: {messageType}, Media FileId: {(mediaFileId != null ? mediaFileId : "[null]")}, Reply Text: {replyText}");
                        // --- Send Media/Text to User Bot ---
                        await SendStaffReplyToUser(userChatId, staffReplyMessage, messageType, mediaFileId, replyText, staffName, cancellationToken);

                        string senderId = staffReplyMessage.From?.Id.ToString() ?? "UnknownStaffId"; // Use "UnknownStaffId" if From is null

                        var staffChatMessage = new ChatMessage(senderId, "Staff", messageType)
                        {
                            TextContent = replyText,  // Could be null for media, and thats ok
                            MediaFileId = mediaFileId, // Will be null for text messages.
                            Timestamp = DateTime.UtcNow
                        };
                        await ticketService.AppendMessageToTicketAsync(repliedTicketId, staffChatMessage);
                        // --- End Log ---


                        await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, $"✅ Reply sent to user (Ticket #{repliedTicketNumber}).", ParseMode.Html, cancellationToken: cancellationToken); // Staff confirmation
                    }

                    else
                    {
                        await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, $"⚠️ Error: Could not find Ticket Data for Ticket Number: {repliedTicketNumber} to send reply.", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, $"⚠️ Invalid ticket number in reply context: {repliedTicketNumber}", cancellationToken: cancellationToken);
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in ProcessStaffReplyAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, "⚠️ Telegram API error processing staff reply.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessStaffReplyAsync: {ex}");
                await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, "⚠️ Error processing staff reply. Please try again.", cancellationToken: cancellationToken);
            }
        }

        private async Task ViewTicketsAsync(long chatId, int pageNumber = 1, int pageSize = 10, int? messageIdToEdit = null, Dictionary<int, string>? existingTicketNumberMap = null, string ticketType = "open", CancellationToken cancellationToken = default)
        {
            try
            {
                List<TicketData>? tickets = null;
                string messagePrefix;

                // --- Cache Check ---
                if (IsCacheValid(ticketType)) // Helper method to check cache validity
                {
                    Console.WriteLine($"Debug: ViewTicketsAsync - Cache hit for ticket type: '{ticketType}'. Retrieving from cache.");
                    if (_activeTicketListCache.TryGetValue(ticketType, out tickets))
                    {
                        messagePrefix = GetMessagePrefix(ticketType); // Helper method to get message prefix
                                                                      // Use cached 'tickets'
                    }
                    else
                    {
                        // Should not happen if IsCacheValid is true, but as a fallback, fetch from service:
                        Console.WriteLine($"Warning: Cache marked as valid but no data found for type '{ticketType}'. Fetching from service.");
                        tickets = await FetchTicketsFromServiceAsync(ticketType); // Helper method to fetch from service and update cache
                        messagePrefix = GetMessagePrefix(ticketType);
                    }
                }
                else
                {
                    Console.WriteLine($"Debug: ViewTicketsAsync - Cache miss or expired for ticket type: '{ticketType}'. Fetching from service.");
                    tickets = await FetchTicketsFromServiceAsync(ticketType); // Helper method to fetch from service and update cache
                    messagePrefix = GetMessagePrefix(ticketType);
                }
                // --- End Cache Check ---


                if (tickets.Count == 0)
                {
                    await botClient.SendMessageSafeAsync(chatId, GetNoTicketsMessage(ticketType), cancellationToken: cancellationToken); // Helper method for "no tickets" message
                    return;
                }

                int totalTickets = tickets.Count;
                int totalPages = (int)Math.Ceiling((double)totalTickets / pageSize);
                pageNumber = Math.Clamp(pageNumber, 1, totalPages);

                int startIndex = (pageNumber - 1) * pageSize;
                int endIndex = Math.Min(startIndex + pageSize, totalTickets);

                List<TicketData> currentPageTickets = tickets.GetRange(startIndex, endIndex - startIndex);

                string messageText = $"{messagePrefix} - Page {pageNumber}/{totalPages}\n\n"; // Use the prefix
                var ticketKeyboardButtons = new List<InlineKeyboardButton[]>();
                var ticketNumberMap = existingTicketNumberMap ?? new Dictionary<int, string>();

                if (existingTicketNumberMap == null) ticketNumberMap.Clear();

                for (int i = 0; i < currentPageTickets.Count; i++)
                {
                    var ticket = currentPageTickets[i];
                    int ticketNumber = startIndex + i + 1; // Keep ticketNumber for mapping

                    ticketNumberMap[ticketNumber] = ticket.TicketId ?? "UnknownTicketId";

                    ticketKeyboardButtons.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData($"Handle - {ticket.TicketId}", $"handle_ticket_by_id-{ticket.TicketId}")
                    });
                }

                var paginationButtonsRow = new List<InlineKeyboardButton>();
                if (pageNumber > 1)
                {
                    // --- CORRECTLY CONSTRUCT CALLBACK DATA ---
                    paginationButtonsRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous Page", $"view_tickets_type-{ticketType}-page-{pageNumber - 1}"));
                    // --- END CORRECT CONSTRUCTION ---
                }
                if (pageNumber < totalPages)
                {
                    // --- CORRECTLY CONSTRUCT CALLBACK DATA ---
                    paginationButtonsRow.Add(InlineKeyboardButton.WithCallbackData("➡️ Next Page", $"view_tickets_type-{ticketType}-page-{pageNumber + 1}"));
                    // --- END CORRECT CONSTRUCTION ---
                }
                if (paginationButtonsRow.Count > 0)
                {
                    ticketKeyboardButtons.Add(paginationButtonsRow.ToArray());
                }

                var inlineKeyboard = new InlineKeyboardMarkup(ticketKeyboardButtons);

                if (messageIdToEdit.HasValue)
                {
                    await botClient.EditMessageText(
                        chatId,
                        messageIdToEdit.Value,
                        messageText,
                        ParseMode.Html,
                        replyMarkup: inlineKeyboard,
                        cancellationToken: cancellationToken);
                    Console.WriteLine($"Debug: ViewTicketsAsync - Edited existing message with messageId: {messageIdToEdit.Value}, page: {pageNumber}, type: {ticketType}");
                }
                else
                {
                    Message sentMessage = await botClient.SendMessage(
                        chatId,
                        messageText,
                        ParseMode.Html,
                        replyMarkup: inlineKeyboard,
                        cancellationToken: cancellationToken);
                    Console.WriteLine($"Debug: ViewTicketsAsync - Sent new message, page: {pageNumber}, type: {ticketType}, messageId: {sentMessage.MessageId}");
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in ViewTicketsAsync (Paged): {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Could not retrieve {ticketType} tickets due to a Telegram API error.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ViewTicketsAsync (Paged): {ex}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Failed to view {ticketType} tickets. Please try again later.", cancellationToken: cancellationToken);
            }
        }

        private bool IsCacheValid(string ticketType)
        {
            return _activeTicketListCache.ContainsKey(ticketType) && DateTime.UtcNow - _lastCacheUpdateTime <= CacheExpirationTime;
        }

        private async Task<List<TicketData>> FetchTicketsFromServiceAsync(string ticketType)
        {
            List<TicketData> tickets;
            switch (ticketType)
            {
                case "open":
                    tickets = await ticketService.GetOpenTicketsAsync();
                    break;
                case "closed":
                    tickets = await ticketService.GetClosedTicketsAsync();
                    break;
                case "all":
                    tickets = await ticketService.GetAllTicketsAsync();
                    break;
                default:
                    return new List<TicketData>(); // Or throw exception if invalid ticketType
            }
            _activeTicketListCache[ticketType] = tickets; // Update cache
            _lastCacheUpdateTime = DateTime.UtcNow; // Update cache update time
            Console.WriteLine($"Debug: FetchTicketsFromServiceAsync - Updated cache for ticket type: '{ticketType}'. Ticket count: {tickets.Count}");
            return tickets;
        }

        private string GetMessagePrefix(string ticketType)
        {
            switch (ticketType)
            {
                case "open":
                    return "🎫 <b>Open Tickets</b>";
                case "closed":
                    return "🎫 <b>Closed Tickets</b>";
                case "all":
                    return "🎫 <b>All Tickets</b>";
                default:
                    return "🎫 <b>Tickets</b>"; // Default prefix
            }
        }

        private string GetNoTicketsMessage(string ticketType)
        {
            return $"🎫 No {ticketType} tickets at the moment.";
        }
        // New method to show options for viewing tickets
        private async Task SendViewTicketsOptionsAsync(long chatId, CancellationToken cancellationToken)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                 new[] { InlineKeyboardButton.WithCallbackData("🟢 Open Tickets", "view_tickets_type-open") },
                new[] { InlineKeyboardButton.WithCallbackData("🔴 Closed Tickets", "view_tickets_type-closed") },
                new[] { InlineKeyboardButton.WithCallbackData("🔵 All Tickets", "view_tickets_type-all") }
            });

            await botClient.SendMessage(
                chatId,
                "🗂️ <b>View Tickets</b>\n\nSelect which tickets you want to view:",
                ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );
        }
        private async Task PerformTicketClosureAsync(long chatId, string ticketIdToClose, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: PerformTicketClosureAsync - Start. chatId: {chatId}, ticketIdToClose: {ticketIdToClose}"); // Log start

                if (!messageIdToEdit.HasValue) // --- ADD EXPLICIT NULL CHECK - Similar to SendCloseTicketConfirmationAsync ---
                {
                    Console.WriteLine("Warning: PerformTicketClosureAsync - messageIdToEdit is null. Cannot edit message."); // Log warning
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message after ticket closure.", cancellationToken: cancellationToken); // Inform staff of error
                    return; // Exit method if messageId is missing
                }

                string closeTicketResult = await ticketService.CloseTicketByTicketIdAsync(ticketIdToClose);
                InvalidateTicketDetailsCache(ticketIdToClose);

                await botClient.EditMessageTextSafeAsync( // Edit confirmation message
                    chatId,
                    messageIdToEdit.Value,
                    $"✅ <b>Ticket {ticketIdToClose} has been closed.</b>\n\n{closeTicketResult}", // Confirmation message with Ticket ID and result
                    ParseMode.Html,
                    replyMarkup: null, // Remove buttons after closing
                    cancellationToken: cancellationToken
                );

                ClearHandlingState(chatId); // Clear handling state for staff

                // --- Optionally notify user (via UserBot) that ticket is closed by staff ---
                // ... (Implementation for user notification - can be added later) ...

                await botClient.SendMessageSafeAsync(logsChannelId, $"🗑️ Ticket with Ticket ID {ticketIdToClose} closed by staff.", cancellationToken: cancellationToken); // Log closure to logs channel

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in PerformTicketClosureAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Telegram API error closing ticket. Ticket may or may not have closed. Telegram API Error: {apiEx.Message}", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformTicketClosureAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error closing ticket. Ticket may or may not have closed due to an unexpected error.", cancellationToken: cancellationToken);
            }
            finally
            {
                ClearHandlingState(chatId); // Ensure handling state is cleared even on error
                Console.WriteLine($"StaffBot: PerformTicketClosureAsync - End."); // Log end
            }
        }

        private async Task SendReopenTicketConfirmationAsync(long chatId, string ticketIdToConfirm, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: SendReopenTicketConfirmationAsync - Start. chatId: {chatId}, ticketIdToConfirm: {ticketIdToConfirm}, messageIdToEdit: {messageIdToEdit}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: SendReopenTicketConfirmationAsync - messageIdToEdit is null. Cannot edit message.");
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message to show reopen ticket confirmation.", cancellationToken: cancellationToken);
                    return;
                }

                var confirmationKeyboard = new InlineKeyboardMarkup(new[]
                {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Yes, Reopen Ticket", $"staff_do_reopen_ticket-{ticketIdToConfirm}"), // Correct Callback for Reopen
                InlineKeyboardButton.WithCallbackData("❌ Cancel", $"staff_cancel_close_ticket-{ticketIdToConfirm}") // Reuse existing Cancel callback
            }
        });

                await botClient.EditMessageText(
                    chatId,
                    messageIdToEdit.Value,
                    $"❓ <b>Are you sure you want to reopen Ticket {ticketIdToConfirm}?</b>",
                    ParseMode.Html,
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken
                );
                Console.WriteLine($"StaffBot: SendReopenTicketConfirmationAsync - Confirmation message sent/edited for ticketId: {ticketIdToConfirm}, messageId: {messageIdToEdit.Value}");

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in SendReopenTicketConfirmationAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error sending reopen ticket confirmation.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendReopenTicketConfirmationAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error sending reopen ticket confirmation. Please try again.", cancellationToken: cancellationToken);
            }
            finally
            {
                Console.WriteLine($"StaffBot: SendReopenTicketConfirmationAsync - End.");
            }
        }
        private async Task PerformTicketReopeningAsync(long chatId, string ticketIdToReopen, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: PerformTicketReopeningAsync - Start. chatId: {chatId}, ticketIdToReopen: {ticketIdToReopen}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: PerformTicketReopeningAsync - messageIdToEdit is null. Cannot edit message.");
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message after ticket reopening.", cancellationToken: cancellationToken);
                    return;
                }

                string reopenTicketResult = await ticketService.ReopenTicketAsync(ticketIdToReopen);
                InvalidateTicketDetailsCache(ticketIdToReopen);

                await botClient.EditMessageTextSafeAsync(
                    chatId,
                    messageIdToEdit.Value,
                    $"✅ <b>Ticket {ticketIdToReopen} has been reopened.</b>\n\n{reopenTicketResult}",
                    ParseMode.Html,
                    replyMarkup: null,
                    cancellationToken: cancellationToken
                );

                // Notify the user
                TicketData? ticketData = await ticketService.GetTicketDataAsync(ticketIdToReopen);
                if (ticketData != null)
                {
                    await botClient.SendMessageSafeAsync(ticketData.ChatId, $"Your ticket (Ticket ID: {ticketIdToReopen}) has been reopened by staff.", cancellationToken: cancellationToken);
                }


                await botClient.SendMessageSafeAsync(logsChannelId, $"♻️ Ticket with Ticket ID {ticketIdToReopen} reopened by staff.", cancellationToken: cancellationToken);
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in PerformTicketReopeningAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Telegram API error reopening ticket. Ticket may or may not have reopened. Telegram API Error: {apiEx.Message}", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformTicketReopeningAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error reopening ticket. Ticket may or may not have reopened due to an unexpected error.", cancellationToken: cancellationToken);
            }
            finally
            {
                Console.WriteLine($"StaffBot: PerformTicketReopeningAsync - End.");
            }
        }
        private async Task PerformTranscriptAndTicketDeletionAsync(long chatId, string ticketIdToDeleteTranscript, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: PerformTranscriptAndTicketDeletionAsynceletionAsync - Start. chatId: {chatId}, ticketIdToTDeleteTranscript: {ticketIdToDeleteTranscript}, messageIdToEdit: {messageIdToEdit}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: PerformTranscriptAndTicketDeletionAsynceletionAsync - messageIdToEdit is null. Cannot edit message.");
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message after ticket deletion.", cancellationToken: cancellationToken);
                    return;
                }

                TicketData? ticketData = await ticketService.GetTicketDataAsync(ticketIdToDeleteTranscript);
                if (ticketData != null)
                {
                    List<ChatMessage> transcriptMessages = await ticketService.GetTicketMessageHistoryAsync(ticketIdToDeleteTranscript);
                    string transcriptText = GenerateTranscriptText(ticketData, transcriptMessages);
                    await botClient.SendMessageSafeAsync(logsChannelId, $"📜 <b>Ticket {ticketIdToDeleteTranscript} Transcript:</b>\n\n{transcriptText}", ParseMode.Html, cancellationToken: cancellationToken); // Send transcript first
                }

                string deleteResult = await ticketService.DeleteTicketAsync(ticketIdToDeleteTranscript);
                InvalidateTicketDetailsCache(ticketIdToDeleteTranscript);

                await botClient.EditMessageTextSafeAsync(
                    chatId,
                    messageIdToEdit.Value,
                    $"🗑️ <b>Ticket {ticketIdToDeleteTranscript} has been DELETED and transcript saved to logs.</b>\n\n{deleteResult}", // Updated confirmation message
                    ParseMode.Html,
                    replyMarkup: null,
                    cancellationToken: cancellationToken
                );

                ClearHandlingState(chatId);
                await botClient.SendMessageSafeAsync(logsChannelId, $"🗑️ Ticket with Ticket ID {ticketIdToDeleteTranscript} DELETED by staff (with transcript).", cancellationToken: cancellationToken);
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in PerformTranscriptAndTicketDeletionAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Telegram API error during ticket termination with transcript. Telegram API Error: {apiEx.Message}", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformTranscriptAndTicketDeletionAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error during ticket termination with transcript due to an unexpected error.", cancellationToken: cancellationToken);
            }
            finally
            {
                ClearHandlingState(chatId);
                Console.WriteLine($"StaffBot: PerformTranscriptAndTicketDeletionAsync - End.");
            }
        }

        private async Task ShowTicketDetailsAsync(long chatId, string ticketId, CancellationToken cancellationToken, string? messagePrefix = null)
        {
            TicketData? selectedTicketData;

            // --- Ticket Details Cache Check ---
            if (IsTicketDetailsCacheValid(ticketId))
            {
                Console.WriteLine($"Debug: ShowTicketDetailsAsync - Cache hit for ticket ID: '{ticketId}'. Retrieving details from cache.");
                selectedTicketData = GetTicketDetailsFromCache(ticketId); // Helper method to get from cache
            }
            else
            {
                Console.WriteLine($"Debug: ShowTicketDetailsAsync - Cache miss or expired for ticket ID: '{ticketId}'. Fetching details from service.");
                selectedTicketData = await FetchTicketDetailsFromServiceAsync(ticketId); // Helper method to fetch from service and update cache
            }
            // --- End Ticket Details Cache Check ---

            if (selectedTicketData != null)
            {
                List<ChatMessage> messageHistory = await ticketService.GetTicketMessageHistoryAsync(selectedTicketData.TicketId ?? "");

                // --- Get both parts from TicketDetailsFormatter ---
                var formattedDetails = TicketDetailsFormatter.FormatTicketDetails(selectedTicketData, messageHistory, _languageParser);
                string detailedTicketInfo = formattedDetails.Item1; // Get basic ticket info
                string messageHistoryText = formattedDetails.Item2; // Get message history text

                InlineKeyboardMarkup replyKeyboardMarkup = GetTicketActionButtons(selectedTicketData);

                // --- Send the BASIC TICKET INFO message FIRST ---
                await botClient.SendMessageSafeAsync(chatId, detailedTicketInfo, ParseMode.Html, replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);

                // --- THEN, send the MESSAGE HISTORY as a *separate* message (if history exists) ---
                if (!string.IsNullOrEmpty(messageHistoryText))
                {
                    Console.WriteLine($"Debug: ShowTicketDetailsAsync - Sending message history separately for Ticket ID: {ticketId}, Chat ID: {chatId}"); // Added log
                    await botClient.SendMessageSafeAsync(chatId, messageHistoryText, ParseMode.Html, cancellationToken: cancellationToken);
                }
                else
                {
                    Console.WriteLine($"Debug: ShowTicketDetailsAsync - No message history to send for Ticket ID: {ticketId}, Chat ID: {chatId}"); // Log when no history
                }
            }
            else
            {
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error: Could not find Ticket Data for Ticket ID: {ticketId}", cancellationToken: cancellationToken);
            }
        }

        private bool IsTicketDetailsCacheValid(string ticketId)
        {
            return _ticketDetailsCache.ContainsKey(ticketId) && _ticketDetailsCacheExpiration.TryGetValue(ticketId, out DateTime expirationTime) && DateTime.UtcNow <= expirationTime;
        }

        private TicketData? GetTicketDetailsFromCache(string ticketId)
        {
            _ticketDetailsCache.TryGetValue(ticketId, out TicketData? ticketData);
            return ticketData;
        }

        private async Task<TicketData?> FetchTicketDetailsFromServiceAsync(string ticketId)
        {
            TicketData? ticketData = await ticketService.GetTicketDataAsync(ticketId);
            if (ticketData != null)
            {
                _ticketDetailsCache[ticketId] = ticketData; // Update cache
                _ticketDetailsCacheExpiration[ticketId] = DateTime.UtcNow.Add(TicketDetailsCacheExpirationTime); // Set expiration time
                Console.WriteLine($"Debug: FetchTicketDetailsFromServiceAsync - Updated cache for ticket ID: '{ticketId}'.");
            }
            return ticketData;
        }

        private async Task SendDeleteTicketConfirmationAsync(long chatId, string ticketIdToConfirm, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: SendDeleteTicketConfirmationAsync - Start. chatId: {chatId}, ticketIdToConfirm: {ticketIdToConfirm}, messageIdToEdit: {messageIdToEdit}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: SendDeleteTicketConfirmationAsync - messageIdToEdit is null. Cannot edit message.");
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message to show delete ticket confirmation.", cancellationToken: cancellationToken);
                    return;
                }

                var confirmationKeyboard = new InlineKeyboardMarkup(new[]
                {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📜 Transcript then Delete", $"staff_do_delete_with_transcript-{ticketIdToConfirm}"),
                InlineKeyboardButton.WithCallbackData("❌ Delete Only", $"staff_delete_only-{ticketIdToConfirm}"),

            },
             new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Cancel", $"staff_cancel_close_ticket-{ticketIdToConfirm}") // Reuse cancel callback
            }

        });

                await botClient.EditMessageText(
                    chatId,
                    messageIdToEdit.Value,
                    $"❓ <b>Are you sure you want to DELETE Ticket {ticketIdToConfirm}?</b>\n\nChoose an option:",  // Clear prompt
                    ParseMode.Html,
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken
                );
                Console.WriteLine($"StaffBot: SendDeleteTicketConfirmationAsync - Confirmation message sent/edited for ticketId: {ticketIdToConfirm}, messageId: {messageIdToEdit.Value}");

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in SendDeleteTicketConfirmationAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error sending Delete Ticket confirmation.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendDeleteTicketConfirmationAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error sending Delete Ticket confirmation. Please try again.", cancellationToken: cancellationToken);
            }
            finally
            {
                Console.WriteLine($"StaffBot: SendDeleteTicketConfirmationAsync - End.");
            }
        }
        private async Task PerformTicketDeletionAsync(long chatId, string ticketIdToDelete, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: PerformTicketDeletionAsync - Start. chatId: {chatId}, ticketIdToDelete: {ticketIdToDelete}, messageIdToEdit: {messageIdToEdit}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: PerformTicketDeletionAsync - messageIdToEdit is null. Cannot edit message.");
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message after ticket deletion.", cancellationToken: cancellationToken);
                    return;
                }

                TicketData? ticketData = await ticketService.GetTicketDataAsync(ticketIdToDelete);
                if (ticketData != null)
                {
                    List<ChatMessage> transcriptMessages = await ticketService.GetTicketMessageHistoryAsync(ticketIdToDelete);
                    string transcriptText = GenerateTranscriptText(ticketData, transcriptMessages);
                }

                string deleteResult = await ticketService.DeleteTicketAsync(ticketIdToDelete);
                InvalidateTicketDetailsCache(ticketIdToDelete);

                await botClient.EditMessageTextSafeAsync(
                    chatId,
                    messageIdToEdit.Value,
                    $"🗑️ <b>Ticket {ticketIdToDelete} has been DELETED.</b>\n\n{deleteResult}", // Updated confirmation message
                    ParseMode.Html,
                    replyMarkup: null,
                    cancellationToken: cancellationToken
                );

                ClearHandlingState(chatId);
                await botClient.SendMessageSafeAsync(logsChannelId, $"🗑️ Ticket with Ticket ID {ticketIdToDelete} DELETED by staff.", cancellationToken: cancellationToken);
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in PerformTicketDeletionAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Telegram API error terminating ticket. Ticket may or may not have been DELETED. Telegram API Error: {apiEx.Message}", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformTicketDeletionAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error terminating ticket. Ticket may or may not have been DELETED due to an unexpected error.", cancellationToken: cancellationToken);
            }
            finally
            {
                ClearHandlingState(chatId);
                Console.WriteLine($"StaffBot: PerformTicketDeletionAsync - End.");
            }
        }

        private async Task RevertToTicketDetailsViewAsync(long chatId, string ticketIdToReopen, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: RevertToTicketDetailsViewAsync - Start. chatId: {chatId}, ticketIdToReopen: {ticketIdToReopen}, messageIdToEdit: {messageIdToEdit}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: RevertToTicketDetailsViewAsync - messageIdToEdit is null. Cannot edit message.");
                    return;
                }

                TicketData? selectedTicketData = await ticketService.GetTicketDataAsync(ticketIdToReopen);

                if (selectedTicketData != null)
                {
                    string detailedTicketInfo = "<b>Ticket Details:</b>\n\n";
                    detailedTicketInfo += $"<b>Ticket ID:</b> <code>{selectedTicketData.TicketId}</code>\n";
                    detailedTicketInfo += $"<b>User ID:</b> <code>{selectedTicketData.ChatId}</code>\n";
                    detailedTicketInfo += $"<b>Status:</b> <code>{selectedTicketData.Status}</code>\n"; //Show status
                    detailedTicketInfo += $"<b>Created:</b> {selectedTicketData.CreatedDate.ToLocalTime():g}\n\n";

                    List<ChatMessage> messageHistory = await ticketService.GetTicketMessageHistoryAsync(selectedTicketData.TicketId ?? "");
                    if (messageHistory.Count > 0)
                    {
                        detailedTicketInfo += "<b>--- Message History ---</b>\n";
                        foreach (ChatMessage chatMessage in messageHistory)
                        {
                            string messageContentDisplay = "";
                            if (chatMessage.MessageType == "Text" && !string.IsNullOrEmpty(chatMessage.TextContent))
                            {
                                detailedTicketInfo += $"<code>{chatMessage.SenderType}: {chatMessage.TextContent}</code>\n";
                            }
                            else if (chatMessage.MessageType == "Image")
                            {
                                detailedTicketInfo += $"<code>{chatMessage.SenderType}: [Image]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                            }
                            else if (chatMessage.MessageType == "Audio")
                            {
                                detailedTicketInfo += $"<code>{chatMessage.SenderType}: [Audio]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                            }
                            else if (chatMessage.MessageType == "Voice")
                            {
                                detailedTicketInfo += $"<code>{chatMessage.SenderType}: [Voice Message]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                            }
                            else if (chatMessage.MessageType == "Video")
                            {
                                detailedTicketInfo += $"<code>{chatMessage.SenderType}: [Video]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                            }
                            detailedTicketInfo += messageContentDisplay;
                        }
                        detailedTicketInfo += "\n";
                    }
                    else
                    {
                        detailedTicketInfo += "<b>--- Message History ---</b>\n";
                        detailedTicketInfo += "<code>No messages yet from user.</code>\n\n";
                    }

                    // Build buttons based on ticket STATUS.
                    InlineKeyboardMarkup replyKeyboardMarkup;
                    if (selectedTicketData.Status == "Open")
                    {
                        replyKeyboardMarkup = new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("💬 Reply to User", $"reply_to_user_by_id-{selectedTicketData.TicketId}")
                            },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("🔄 Done for now", $"staff_switch_ticket-{selectedTicketData.TicketId}"),
                                InlineKeyboardButton.WithCallbackData("❌ Close Ticket", $"staff_confirm_close_ticket-{selectedTicketData.TicketId}"),
                                //InlineKeyboardButton.WithCallbackData("🗑️ Delete Ticket", $"staff_confirm_delete_ticket-{selectedTicketData.TicketId}")
                            }
                        });
                    }
                    else if (selectedTicketData.Status == "Closed")
                    {
                        replyKeyboardMarkup = new InlineKeyboardMarkup(new[]
                       {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("♻️ Reopen Ticket", $"staff_reopen_ticket-{selectedTicketData.TicketId}")  // Only show Reopen for Closed
                            }

                        });
                    }
                    else //Should never happen
                    {
                        replyKeyboardMarkup = new InlineKeyboardMarkup(new[]
                       {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("ERROR", $"error")
                            }
                        });
                    }


                    await botClient.EditMessageText(
                        chatId,
                        messageIdToEdit.Value,
                        detailedTicketInfo,
                        ParseMode.Html,
                        replyMarkup: replyKeyboardMarkup,
                        cancellationToken: cancellationToken
                    );
                    Console.WriteLine($"StaffBot: RevertToTicketDetailsViewAsync - Reverted to ticket details view for ticketId: {ticketIdToReopen}, messageId: {messageIdToEdit.Value}");

                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error: Could not find Ticket Data for Ticket ID: {ticketIdToReopen} to cancel closure.", cancellationToken: cancellationToken);
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in RevertToTicketDetailsViewAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error reverting to ticket details view.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RevertToTicketDetailsViewAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error reverting to ticket details view. Please try again.", cancellationToken: cancellationToken);
            }
            finally
            {
                Console.WriteLine($"StaffBot: RevertToTicketDetailsViewAsync - End.");
            }
        }
        private async Task HandleAdminCommandAsync(Message message, CancellationToken cancellationToken)
        {
            string text = message.Text ?? "";
            string[] parts = text.Split(' ');
            if (parts.Length > 1)
            {
                string command = parts[1].ToLower();
                if (command == "broadcast")
                {
                    if (parts.Length > 2)
                    {
                        string broadcastMessage = string.Join(" ", parts.Skip(2));
                        await BroadcastMessageAsync(broadcastMessage, cancellationToken);
                    }
                    else
                    {
                        await botClient.SendMessageSafeAsync(message.Chat.Id, "Usage: /admin broadcast <message>", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await botClient.SendMessageSafeAsync(message.Chat.Id, "Unknown admin command.", cancellationToken: cancellationToken);
                }
            }
            else
            {
                await botClient.SendMessageSafeAsync(message.Chat.Id, "Available admin commands: /admin broadcast <message>", cancellationToken: cancellationToken);
            }
        }
        private async Task HandleTicketSelectionByTicketIdAsync(long chatId, string ticketIdToHandle, CancellationToken cancellationToken)
        {
            try
            {
                await ShowTicketDetailsAsync(chatId, ticketIdToHandle, cancellationToken); // Call ShowTicketDetailsAsync directly
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in HandleTicketSelectionByTicketIdAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error retrieving ticket details.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleTicketSelectionByTicketIdAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error retrieving ticket details. Please try again.", cancellationToken: cancellationToken);
            }
            finally
            {
                Console.WriteLine($"StaffBot: HandleTicketSelectionByTicketIdAsync - End.");
            }
        }

        private async Task SendTicketTranscriptToLogsAsync(long chatId, string ticketIdTranscript, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: SendTicketTranscriptToLogsAsync - Start. chatId: {chatId}, ticketIdTranscript: {ticketIdTranscript}, messageIdToEdit: {messageIdToEdit}");

                TicketData? ticketData = await ticketService.GetTicketDataAsync(ticketIdTranscript);
                if (ticketData != null)
                {
                    List<ChatMessage> transcriptMessages = await ticketService.GetTicketMessageHistoryAsync(ticketIdTranscript);
                    string transcriptText = GenerateTranscriptText(ticketData, transcriptMessages);
                    await botClient.SendMessageSafeAsync(logsChannelId, $"📜 <b>Ticket {ticketIdTranscript} Transcript:</b>\n\n{transcriptText}", ParseMode.Html, cancellationToken: cancellationToken);

                    await botClient.SendMessageSafeAsync(chatId, $"📜 Transcript for Ticket <code>{ticketIdTranscript}</code> sent to logs channel.", ParseMode.Html, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error: Could not find Ticket Data for Ticket ID: {ticketIdTranscript} to generate transcript.", cancellationToken: cancellationToken);
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in SendTicketTranscriptToLogsAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Telegram API error sending ticket transcript. Telegram API Error: {apiEx.Message}", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendTicketTranscriptToLogsAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, $"⚠️ Error sending ticket transcript due to an unexpected error.", cancellationToken: cancellationToken);
            }
            finally
            {
                // No need to clear handling state here, just sending transcript
                if (messageIdToEdit.HasValue)
                    await botClient.DeleteMessage(chatId, messageIdToEdit.Value, cancellationToken); // Optionally remove "Ticket Details" message after transcript is requested
                Console.WriteLine($"StaffBot: PerformTranscriptAndTicketDeletionAsync - End.");
            }
        }

        private async Task PerformTicketSwitchAsync(long chatId, string ticketIdToSwitch, int messageId, CancellationToken cancellationToken)
        {
            if (ticketService.ActiveTicketsChatIdToTicketIdMap.TryRemove(chatId, out string? removedTicketId)) // Corrected Access
            {
                // Also remove from HandlingTicketForStaff when switching.
                _handlingTicketForStaff.TryRemove(chatId, out _);
                await botClient.EditMessageTextSafeAsync(chatId, messageId, string.Format(_languageParser.GetMessage("youAreNoLongerHandlingTicketMessage"), ticketIdToSwitch), ParseMode.Html, replyMarkup: null, cancellationToken: cancellationToken);
                Console.WriteLine($"Staff member {chatId} switched from ticket {ticketIdToSwitch}.");
            }
            else
            {
                await botClient.SendMessageSafeAsync(chatId, string.Format(_languageParser.GetMessage("youAreNotCurrentlyHandlingTicketMessage"), ticketIdToSwitch), cancellationToken: cancellationToken);
            }
        }

        private async Task SendCloseTicketConfirmationAsync(long chatId, string ticketIdToConfirm, int? messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: SendCloseTicketConfirmationAsync - Start. chatId: {chatId}, ticketIdToConfirm: {ticketIdToConfirm}, messageIdToEdit: {messageIdToEdit}");

                if (!messageIdToEdit.HasValue)
                {
                    Console.WriteLine("Warning: SendCloseTicketConfirmationAsync - messageIdToEdit is null. Cannot edit message.");
                    await botClient.SendMessageSafeAsync(chatId, "⚠️ Error: Cannot edit message to show close/delete options.", cancellationToken: cancellationToken);
                    return;
                }

                var confirmationKeyboard = new InlineKeyboardMarkup(new[]
                {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Close Ticket", $"staff_do_close_ticket-{ticketIdToConfirm}"), // Close
                InlineKeyboardButton.WithCallbackData("🗑️ Delete Ticket", $"staff_confirm_delete_ticket-{ticketIdToConfirm}"), // Now goes to DELETE confirmation
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Cancel", $"staff_cancel_close_ticket-{ticketIdToConfirm}")  // Cancel
            }
        });

                await botClient.EditMessageText(
                    chatId,
                    messageIdToEdit.Value,
                    $"❓ <b>Ticket {ticketIdToConfirm} - Close or Delete?</b>\n\nChoose an action:", // Updated prompt
                    ParseMode.Html,
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken
                );
                Console.WriteLine($"StaffBot: SendCloseTicketConfirmationAsync - Close/Delete options sent for ticketId: {ticketIdToConfirm}, messageId: {messageIdToEdit.Value}");

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in SendCloseTicketConfirmationAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error sending close/delete options.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendCloseTicketConfirmationAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error sending close/delete options. Please try again.", cancellationToken: cancellationToken);
            }
            finally
            {
                Console.WriteLine($"StaffBot: SendCloseTicketConfirmationAsync - End.");

            }
        }
        private async Task CloseTicketByStaffAsync(string ticketIdToClose, CancellationToken cancellationToken)
        {
            string closeTicketResult = await ticketService.CloseTicketByTicketIdAsync(ticketIdToClose);
            try
            {
                await botClient.SendMessageSafeAsync(logsChannelId, $"🗑️ Ticket with Ticket ID {ticketIdToClose} closed by staff.\n{closeTicketResult}", cancellationToken: cancellationToken);

                // --- Safe Handling of Staff Notification ---
                // Find the staff member who is handling this ticket (if any).
                var handlingStaffEntry = _handlingTicketForStaff.FirstOrDefault(x => x.Value == ticketIdToClose);

                if (!EqualityComparer<KeyValuePair<long, string>>.Default.Equals(handlingStaffEntry, default(KeyValuePair<long, string>)))
                {
                    // If a staff member *was* handling the ticket, notify them.
                    await botClient.SendMessageSafeAsync(handlingStaffEntry.Key, $"🗑️ You have closed Ticket ID {ticketIdToClose}.\n{closeTicketResult}", cancellationToken: cancellationToken);
                    ClearHandlingState(handlingStaffEntry.Key); // Clear handling state for staff
                }
                else
                {
                    Console.WriteLine($"Debug: CloseTicketByStaffAsync - No staff member found handling Ticket ID: {ticketIdToClose} to notify.");
                }
                // --- End Safe Handling ---
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error sending close ticket confirmation to staff: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(logsChannelId, $"⚠️ Error closing ticket and sending confirmation. Ticket may or may not have closed. Telegram API Error: {apiEx.Message}", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing ticket by staff: {ex}");
                await botClient.SendMessageSafeAsync(logsChannelId, $"⚠️ Error closing ticket and sending confirmation. Ticket may or may not have closed due to an unexpected error.", cancellationToken: cancellationToken);
            }
        }


        private async Task ProcessContinuousStaffReplyAsync(Message staffReplyMessage, string handledTicketId, CancellationToken cancellationToken)
        {
            string? replyText = staffReplyMessage.Text;
            string messageType = "Text";
            string? mediaFileId = null;


            try
            {
                Console.WriteLine($"Debug (StaffBot): ProcessContinuousStaffReplyAsync - Start. Staff ChatId: {staffReplyMessage.Chat.Id}, Handled TicketId: {handledTicketId}");
                // --- 1. Determine Message Type using MessageTypeHelper ---
                messageType = MessageTypeHelper.GetMessageType(staffReplyMessage);
                replyText = MessageTypeHelper.GetMessageText(staffReplyMessage); // Caption or Text
                mediaFileId = MessageTypeHelper.GetMediaFileId(staffReplyMessage);


                if (string.IsNullOrEmpty(replyText) && string.IsNullOrEmpty(mediaFileId))
                {
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, _languageParser.GetMessage("staffReplyMessageEmptyMessage"), cancellationToken: cancellationToken);
                    return;
                }

                TicketData? repliedTicketData = await ticketService.GetTicketDataAsync(handledTicketId);
                if (repliedTicketData != null)
                {
                    long userChatId = repliedTicketData.ChatId;
                    string staffName = (staffReplyMessage.From?.FirstName ?? "") + " " + (staffReplyMessage.From?.LastName ?? "");
                    staffName = string.IsNullOrWhiteSpace(staffName) ? "Staff" : staffName.Trim();

                    await SendStaffReplyToUser(userChatId, staffReplyMessage, messageType, mediaFileId, replyText, staffName, cancellationToken);

                    string senderId = staffReplyMessage.From?.Id.ToString() ?? "UnknownStaffId";
                    var staffChatMessage = new ChatMessage(senderId, "Staff", messageType)
                    {
                        TextContent = replyText,
                        MediaFileId = mediaFileId,
                        Timestamp = DateTime.UtcNow
                    };

                    await ticketService.AppendMessageToTicketAsync(handledTicketId, staffChatMessage);
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, string.Format(_languageParser.GetMessage("replySentToUserMessage"), handledTicketId), ParseMode.Html, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, string.Format(_languageParser.GetMessage("errorCouldNotFindTicketDataToSendReplyMessage"), handledTicketId), cancellationToken: cancellationToken);
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in ProcessContinuousStaffReplyAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, _languageParser.GetMessage("telegramApiErrorProcessingStaffReplyMessage"), cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessContinuousStaffReplyAsync: {ex}");
                await botClient.SendMessageSafeAsync(staffReplyMessage.Chat.Id, _languageParser.GetMessage("errorProcessingStaffReplyMessage"), cancellationToken: cancellationToken);
            }
        }


        private async Task SearchTicketAsync(long chatId, string ticketIdToSearch, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"StaffBot: SearchTicketAsync - chatId: {chatId}, ticketIdToSearch: {ticketIdToSearch} - Starting ticket search."); // Log search start

                List<TicketData> searchResults = await ticketService.SearchTicketsAsync(ticketIdToSearch); // Call TicketService search method
                Console.WriteLine($"StaffBot: SearchTicketAsync - chatId: {chatId} - Search results count: {searchResults.Count}"); // Log results count

                if (searchResults.Count > 0)
                {
                    string messageText = "🔍 <b>Search Results:</b>\n\n";
                    var ticketKeyboardButtons = new List<InlineKeyboardButton[]>();
                    int ticketNumber = 1; // Use simple numbering for search results

                    foreach (var ticket in searchResults)
                    {
                        messageText += $"<b>{ticketNumber}. Ticket ID:</b> <code>{ticket.TicketId}</code>\n";
                        messageText += $"<b>User ID:</b> <code>{ticket.ChatId}</code>\n";
                        messageText += $"<b>Status:</b> <code>{ticket.Status}</code>\n";
                        messageText += $"<b>Created:</b> {ticket.CreatedDate.ToLocalTime():g}\n";

                        // Display first message as summary (if any text messages exist)
                        ChatMessage? firstMessage = ticket.Messages?.FirstOrDefault(msg => msg.MessageType == "Text" && !string.IsNullOrEmpty(msg.TextContent));
                        if (firstMessage != null)
                        {
                            // --- Null-safe access to firstMessage.TextContent ---
                            string? summaryText = firstMessage.TextContent; // Assign to a temporary variable
                            if (!string.IsNullOrEmpty(summaryText)) // Check if TextContent is not null or empty
                            {
                                messageText += $"<b>Summary:</b> <code>{summaryText.Substring(0, Math.Min(summaryText.Length, 50))}...</code>\n"; // Use summaryText for Substring
                            }
                            // --- End Null-safe access ---
                        }
                        messageText += "\n";

                        ticketKeyboardButtons.Add(new[]
                        {
                            InlineKeyboardButton.WithCallbackData($"Handle Ticket {ticket.TicketId}", $"handle_ticket_by_id-{ticket.TicketId}"),
                            InlineKeyboardButton.WithCallbackData($"Delete Ticket {ticket.TicketId}", $"staff_confirm_delete_ticket-{ticket.TicketId}") // Add delete button

                        });
                        ticketNumber++;
                    }

                    var inlineKeyboard = new InlineKeyboardMarkup(ticketKeyboardButtons);
                    await botClient.SendMessage(chatId, messageText, ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessageSafeAsync(
                        chatId,
                        $"🔍 <b>No tickets found</b> matching: <code>{ticketIdToSearch}</code>\n\n" +
                        "Please try a different Ticket ID or keyword.",
                        ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                }
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in SearchTicketAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error during ticket search.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SearchTicketAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error during ticket search. Please try again.", cancellationToken: cancellationToken);
            }
            Console.WriteLine($"StaffBot: SearchTicketAsync - End."); // Log search end
        }


        private async Task SendStaffReplyToUser(long userChatId, Message staffReplyMessage, string messageType, string? mediaFileId, string? replyText, string staffName, CancellationToken cancellationToken)
        {
            
            try
            {
                Console.WriteLine($"Debug (StaffBot): SendStaffReplyToUser - Sending to User ChatId: {userChatId}, Staff ChatId: {staffReplyMessage.Chat.Id}, Message Type: {messageType}, MediaFileId: {mediaFileId ?? "[null]"}");

                Stream? mediaStream = null; // Initialize mediaStream to null

                if (!string.IsNullOrEmpty(mediaFileId) && messageType != "Text") // Download media only if FileId is present and it's not text
                {
                    try
                    {
                        Console.WriteLine($"Debug: Requesting file with FileId: {mediaFileId}");

                        // Gets the file from Telegram
                        var file = await botClient.GetFile(mediaFileId, cancellationToken);

                        if (string.IsNullOrEmpty(file.FilePath))
                        {
                            Console.WriteLine("Error: FilePath is null or empty.");
                            return;
                        }
                        DotEnv.Load();
                        // Constructs the correct file URL
                        var botToken = Environment.GetEnvironmentVariable("STAFF_BOT_TOKEN"); 
                        var fileUrl = $"https://api.telegram.org/file/bot{botToken}/{file.FilePath}";

                        // Download the file using HttpClient
                        mediaStream = new MemoryStream();
                        using (HttpClient httpClient = new HttpClient())
                        {
                            var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                            await mediaStream.WriteAsync(fileBytes, 0, fileBytes.Length, cancellationToken);
                        }

                        // Reset stream position
                        mediaStream.Position = 0;

                        Console.WriteLine($"Debug: Media file downloaded successfully for FileId: {mediaFileId}");
                    }
                    catch (Exception downloadEx)
                    {
                        Console.WriteLine($"Error downloading media file from Telegram API: {downloadEx}");
                        mediaStream?.Dispose(); // Ensure stream is disposed in case of download error
                        mediaStream = null; // Set to null to indicate download failure
                                            // Consider handling download errors more gracefully (e.g., send error message to staff/logs)
                    }
                }


                // Delegate sending to UserBot, now passing the mediaStream
                await userBotInstance.ReceiveStaffReplyAsync(userChatId, staffReplyMessage, messageType, mediaStream, replyText, staffName, cancellationToken); // Passing mediaStream
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendStaffReplyToUser: {ex}");
                // Exception handling remains the same
            }
        }
        private async Task BroadcastMessageAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                await botClient.SendMessageSafeAsync(logsChannelId, $"📢 Broadcast from Admin:\n{message}", cancellationToken: cancellationToken); // Use logsChannelId for broadcast
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error broadcasting message: {apiEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting message: {ex}");
            }
        }

        // NEW:  Centralized Button Logic
        private InlineKeyboardMarkup GetTicketActionButtons(TicketData ticketData)
        {
            List<InlineKeyboardButton[]> buttons = new List<InlineKeyboardButton[]>();

            if (ticketData.Status == "Open")
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("replyToUserMessage"), $"reply_to_user_by_id-{ticketData.TicketId}")
                });
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("switchTicket"), $"staff_switch_ticket-{ticketData.TicketId}"),
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("closeTicketMessage"), $"staff_confirm_close_ticket-{ticketData.TicketId}"), // Combined Close/Delete
                });
            }
            else if (ticketData.Status == "Closed")
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(_languageParser.GetMessage("reopenTicketMessage"), $"staff_reopen_ticket-{ticketData.TicketId}")
                });
            }
            // No 'else' needed, as an empty button list is perfectly valid.

            return new InlineKeyboardMarkup(buttons);
        }

        private async Task InitiateTicketSearchAsync(long chatId, CancellationToken cancellationToken)
        {
            try
            {
                await botClient.SendMessageSafeAsync(
                    chatId,
                    "🔍 <b>Search Tickets</b>\n\n" +
                    "Please enter the <b>Ticket ID</b> you want to search for (e.g., <code>TCK-00123</code>):",
                    ParseMode.Html,
                    cancellationToken: cancellationToken
                );

                // --- Future Enhancement: Set a state to indicate bot is waiting for Ticket ID input from this staff member ---
                // --- For now, we'll just rely on message content detection in HandleMessageAsync ---

            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error in InitiateTicketSearchAsync: {apiEx.Message}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Telegram API error initiating ticket search.", cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitiateTicketSearchAsync: {ex}");
                await botClient.SendMessageSafeAsync(chatId, "⚠️ Error initiating ticket search. Please try again.", cancellationToken: cancellationToken);
            }
        }

        private void InvalidateTicketDetailsCache(string ticketId)
        {
            if (_ticketDetailsCache.ContainsKey(ticketId))
            {
                _ticketDetailsCache.TryRemove(ticketId, out _); // Remove from data cache
                _ticketDetailsCacheExpiration.Remove(ticketId); // Remove from expiration tracking
                Console.WriteLine($"Debug: InvalidateTicketDetailsCache - Ticket details cache invalidated for Ticket ID: '{ticketId}'.");
            }
            else
            {
                Console.WriteLine($"Debug: InvalidateTicketDetailsCache - No cache entry found to invalidate for Ticket ID: '{ticketId}'."); // Log if no cache entry to invalidate (might be normal)
            }
        }

        private bool IsTicketIdFormat(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string trimmedText = text.Trim().ToUpperInvariant();
            return trimmedText.StartsWith("TCK-") && trimmedText.Substring(4).All(char.IsDigit); // Check for "TCK-" prefix and digits after
        }

        public (bool IsHandling, long StaffChatId) IsHandlingTicket(string ticketId)
        {
            var handlingStaff = _handlingTicketForStaff.FirstOrDefault(kvp => kvp.Value == ticketId);

            if (!EqualityComparer<KeyValuePair<long, string>>.Default.Equals(handlingStaff, default(KeyValuePair<long, string>)))
            {
                return (true, handlingStaff.Key);
            }
            return (false, 0);
        }


        private bool IsAdmin(long userId)
        {
            return adminUserIds.Contains(userId);
        }

        private void ClearHandlingState(long staffChatId)
        {
            _handlingTicketForStaff.TryRemove(staffChatId, out _);
            ticketService.ActiveTicketsChatIdToTicketIdMap.TryRemove(staffChatId, out _); // Corrected Access
            Console.WriteLine($"Debug: Handling state cleared for staff ChatId {staffChatId}.");
        }

        private string GenerateTranscriptText(TicketData ticketData, List<ChatMessage> messages)
        {
            string transcript = $"<b>Ticket ID:</b> <code>{ticketData.TicketId}</code>\n";
            transcript += $"<b>User ID:</b> <code>{ticketData.ChatId}</code>\n";
            transcript += $"<b>Created Date:</b> {ticketData.CreatedDate.ToLocalTime():g}\n";
            transcript += $"<b>Status:</b> <code>{ticketData.Status}</code>\n\n";
            transcript += "<b>--- Conversation Transcript ---</b>\n";

            foreach (var msg in messages)
            {
                string senderName = msg.SenderType == "User" ? "User" : "Staff";
                string messageContent = "";
                if (msg.MessageType == "Text")
                {
                    messageContent = msg.TextContent ?? "";
                }
                else if (msg.MessageType == "Image")
                {
                    messageContent = "[Image]";
                } // Add cases for other media types if needed in transcript
                else if (msg.MessageType == "Audio") { messageContent = "[Audio]"; }
                else if (msg.MessageType == "Voice") { messageContent = "[Voice Message]"; }
                else if (msg.MessageType == "Video") { messageContent = "[Video]"; }
                transcript += $"<b>{senderName}:</b> {messageContent} <i>({msg.Timestamp.ToLocalTime():g})</i>\n";
            }
            return transcript;
        }

    }
}