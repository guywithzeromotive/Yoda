using System;
using System.Collections.Generic;

namespace Yoda_Bot.Models
{
    public class TicketData
    {
        public string? TicketId { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public long ChatId { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>(); // Changed to List<ChatMessage>
    }
}