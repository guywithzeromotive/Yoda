using System;

namespace Yoda_Bot.Models
{
    public class ChatMessage
    {
        public string? SenderId { get; set; }
        public string? SenderType { get; set; } // Made nullable
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? MessageType { get; set; } // Made nullable
        public string? TextContent { get; set; } // For text messages
        public string? MediaFileId { get; set; } // Telegram File ID for media messages
        // Future: Could add properties for file captions, thumbnails, etc. if needed.

        public ChatMessage() { } // Default constructor for Firebase deserialization

        public ChatMessage(string senderId, string senderType, string messageType)
        {
            SenderId = senderId;
            SenderType = senderType;
            MessageType = messageType;
            Timestamp = DateTime.UtcNow;
        }
    }
}