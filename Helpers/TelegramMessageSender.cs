using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System;
using System.Threading.Tasks;

namespace Yoda_Bot.Helpers
{
    public static class TelegramMessageSender
    {
        public static async Task<bool> SendMessageSafeAsync(
            this ITelegramBotClient botClient,
            ChatId chatId,
            string text,
            ParseMode? parseMode = null,
            bool? disableWebPagePreview = null,
            bool? disableNotification = null,
            int? replyToMessageId = null,
            bool? allowSendingWithoutReply = null,
            string? quote = null, // NEW: Quote text parameter
            ParseMode? quoteParseMode = null, // NEW: Quote parse mode parameter
            ReplyMarkup? replyMarkup = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var replyParams = replyToMessageId.HasValue || !string.IsNullOrEmpty(quote) // Check for replyToMessageId OR quote
                    ? new ReplyParameters()
                    {
                        MessageId = replyToMessageId.GetValueOrDefault(), // Use GetValueOrDefault to handle nullables
                        AllowSendingWithoutReply = allowSendingWithoutReply ?? false,
                        Quote = quote, // Set quote text
                        QuoteParseMode = quoteParseMode ?? ParseMode.Markdown
                    }
                    : null;

                await botClient.SendMessage( // Corrected: Removed Async suffix if needed (check CS1061 again after this fix)
                    chatId: chatId,
                    text: text,
                    parseMode: parseMode ?? ParseMode.Markdown, // Use null-coalescing operator for ParseMode
                    disableNotification: disableNotification ?? false, // Use null-coalescing operator for bool?
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = disableWebPagePreview ?? false }, // Use null-coalescing operator for bool?
                    replyMarkup: replyMarkup,
                    replyParameters: replyParams,
                    cancellationToken: cancellationToken
                );
                return true;
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error sending message to chat {chatId}: {apiEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to chat {chatId}: {ex}");
                return false;
            }
        }


        public static async Task<bool> EditMessageTextSafeAsync(
            this ITelegramBotClient botClient,
            ChatId chatId,
            int messageId,
            string text,
            ParseMode? parseMode = null,
            bool? disableWebPagePreview = null,
            ReplyMarkup? replyMarkup = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await botClient.EditMessageText(
                    chatId: chatId,
                    messageId: messageId,
                    text: text,
                    parseMode: parseMode ?? ParseMode.Markdown, // Use null-coalescing operator for ParseMode
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = disableWebPagePreview ?? false }, // Use null-coalescing operator for bool?
                    replyMarkup: replyMarkup as InlineKeyboardMarkup,
                    cancellationToken: cancellationToken
                );

                return true;
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error editing message {messageId} in chat {chatId}: {apiEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error editing message {messageId} in chat {chatId}: {ex}");
                return false;
            }
        }


        public static async Task<bool> SendPhotoSafeAsync( // Modified SendPhotoSafeAsync to use ReplyParameters and quote
            this ITelegramBotClient botClient,
            ChatId chatId,
            InputFile photo,
            string? caption = null,
            ParseMode? parseMode = null,
            bool? disableNotification = null,
            int? replyToMessageId = null,
            bool? allowSendingWithoutReply = null,
            string? quote = null, // NEW: Quote text parameter (also for photos)
            ParseMode? quoteParseMode = null, // NEW: Quote parse mode parameter (also for photos)
            ReplyMarkup? replyMarkup = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var replyParams = replyToMessageId.HasValue || !string.IsNullOrEmpty(quote) // Check for replyToMessageId OR quote
                    ? new ReplyParameters()
                    {
                        MessageId = replyToMessageId.GetValueOrDefault(),
                        AllowSendingWithoutReply = allowSendingWithoutReply ?? false,
                        Quote = quote, // Set quote text
                        QuoteParseMode = quoteParseMode ?? ParseMode.Markdown
                    }
                    : null;


                await botClient.SendPhoto( // Use SendPhotoAsync (base method) - CORRECTED
                    chatId: chatId,
                    photo: photo,
                    caption: caption,
                    parseMode: parseMode ?? ParseMode.Markdown, // Use null-coalescing operator for ParseMode
                    disableNotification: disableNotification ?? false, // Use null-coalescing operator for bool?
                    replyMarkup: replyMarkup as InlineKeyboardMarkup,
                    replyParameters: replyParams, // Use ReplyParameters here
                    cancellationToken: cancellationToken
                );
                return true;
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error sending photo to chat {chatId}: {apiEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending photo to chat {chatId}: {ex}");
                return false;
            }
        }


        public static async Task<bool> DeleteMessageSafeAsync(
            this ITelegramBotClient botClient,
            ChatId chatId,
            int messageId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await botClient.DeleteMessage( // Corrected: Removed Async suffix as per CS0618 error
                    chatId: chatId,
                    messageId: messageId,
                    cancellationToken: cancellationToken
                );
                return true;
            }
            catch (ApiRequestException apiEx)
            {
                Console.WriteLine($"Telegram API Error deleting message {messageId} in chat {chatId}: {apiEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting message {messageId} in chat {chatId}: {ex}");
                return false;
            }
        }
    }
}