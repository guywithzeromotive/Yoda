using System.Collections.Generic;
using Yoda_Bot.Models;
using Yoda_Bot.Utils;
using Telegram.Bot.Types.Enums;

namespace Yoda_Bot.Helpers
{
    public static class TicketDetailsFormatter
    {
        public static Tuple<string, string> FormatTicketDetails(TicketData ticketData, List<ChatMessage> messageHistory, LanguageParser languageParser) // Modified return type
        {
            string detailedTicketInfo = languageParser.GetMessage("ticketDetailsMessage");
            detailedTicketInfo += string.Format(languageParser.GetMessage("ticketIdMessage"), ticketData.TicketId);
            detailedTicketInfo += string.Format(languageParser.GetMessage("userIdMessage"), ticketData.ChatId);
            detailedTicketInfo += $"<b>Status:</b> <code>{ticketData.Status}</code>\n";
            detailedTicketInfo += string.Format(languageParser.GetMessage("createdMessage"), ticketData.CreatedDate.ToLocalTime());

            string messageHistoryText = ""; // Initialize messageHistoryText

            if (messageHistory.Count > 0)
            {
                messageHistoryText += languageParser.GetMessage("messageHistoryMessage"); // "<b>--- Message History ---</b>\n"; // Localized title
                foreach (ChatMessage chatMessage in messageHistory)
                {
                    string messageContentDisplay = "";
                    if (chatMessage.MessageType == "Text" && !string.IsNullOrEmpty(chatMessage.TextContent))
                    {
                        messageContentDisplay = $"<code>{chatMessage.SenderType}: {chatMessage.TextContent}</code>\n";
                    }
                    else if (chatMessage.MessageType == "Image")
                    {
                        messageContentDisplay = $"<code>{chatMessage.SenderType}: [Image]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                    }
                    else if (chatMessage.MessageType == "Audio")
                    {
                        messageContentDisplay = $"<code>{chatMessage.SenderType}: [Audio]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                    }
                    else if (chatMessage.MessageType == "Voice")
                    {
                        messageContentDisplay = $"<code>{chatMessage.SenderType}: [Voice Message]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                    }
                    else if (chatMessage.MessageType == "Video")
                    {
                        messageContentDisplay = $"<code>{chatMessage.SenderType}: [Video]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                    }
                    else if (chatMessage.MessageType == "Document")
                    {
                        messageContentDisplay = $"<code>{chatMessage.SenderType}: [Document]</code> File ID: <code>{chatMessage.MediaFileId}</code>\n";
                    }
                    messageHistoryText += messageContentDisplay;
                }
                messageHistoryText += "\n";
            }
            else
            {
                messageHistoryText += languageParser.GetMessage("messageHistoryMessage"); // "<b>--- Message History ---</b>\n"; // Localized title
                messageHistoryText += languageParser.GetMessage("noMessagesYetMessage"); // "<code>No messages yet from user.</code>\n\n"; // Localized message
            }
            return new Tuple<string, string>(detailedTicketInfo, messageHistoryText); // Return both parts as a Tuple
        }
    }
}