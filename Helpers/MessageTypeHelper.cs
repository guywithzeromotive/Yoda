using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Yoda_Bot.Helpers
{
    public static class MessageTypeHelper
    {
        public static string GetMessageType(Message message)
        {
            if (message.Text != null)
            {
                return "Text";
            }
            else if (message.Photo != null && message.Photo.Length > 0)
            {
                return "Image";
            }
            else if (message.Audio != null)
            {
                return "Audio";
            }
            else if (message.Voice != null)
            {
                return "Voice";
            }
            else if (message.Video != null)
            {
                return "Video";
            }
            else if (message.Document != null)
            {
                return "Document";
            }
            else
            {
                return "Unknown";
            }
        }

        public static string? GetMessageText(Message message)
        {
            if (message.Text != null)
            {
                return message.Text;
            }
            else if (message.Caption != null)
            {
                return message.Caption;
            }
            return null; // No text content for non-text messages without captions
        }

        public static string? GetMediaFileId(Message message)
        {
            string messageType = GetMessageType(message); // Get message type for logging context
            string? mediaFileId = null;

            if (messageType == "Image")
            {
                mediaFileId = message.Photo?.LastOrDefault()?.FileId;
            }
            else if (messageType == "Audio")
            {
                mediaFileId = message.Audio?.FileId;
            }
            else if (messageType == "Voice")
            {
                mediaFileId = message.Voice?.FileId;
            }
            else if (messageType == "Video")
            {
                mediaFileId = message.Video?.FileId;
            }
            else if (messageType == "Document")
            {
                mediaFileId = message.Document?.FileId;
            }

            Console.WriteLine($"Debug (MessageTypeHelper): GetMediaFileId - Message Type: {messageType}, Extracted FileId: {(mediaFileId != null ? mediaFileId : "[null]")}"); // Added logging

            return mediaFileId;
        }
    }
}