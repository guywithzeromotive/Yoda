using Firebase.Database;
using Firebase.Database.Query;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yoda_Bot.Models;
using Newtonsoft.Json;
using System.Diagnostics; // ADD THIS

namespace Yoda_Bot.Services
{
    public class TicketService
    {
        private readonly FirebaseClient _firebaseClient;
        public ConcurrentDictionary<long, string> ActiveTicketsChatIdToTicketIdMap { get; } = new ConcurrentDictionary<long, string>();
        private readonly Dictionary<long, string> _userLanguageCache = new Dictionary<long, string>();

        public TicketService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        }

        public async Task InitializeAsync()
        {
            await LoadActiveTicketsFromFirebaseAsync();
        }

        private async Task<int> GetNextTicketNumberAsync()
        {
            int maxRetries = 3;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    Stopwatch sw = Stopwatch.StartNew(); // START TIMER
                    var counterRef = _firebaseClient.Child("ticketCounter");
                    var currentValueSnapshot = await counterRef.OnceSingleAsync<int?>();
                    int nextValue = (currentValueSnapshot ?? 0) + 1;
                    await counterRef.PutAsync(nextValue);
                    var confirmedValueSnapshot = await counterRef.OnceSingleAsync<int?>();
                    sw.Stop(); // STOP TIMER
                    Console.WriteLine($"Firebase GetNextTicketNumberAsync took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

                    if (confirmedValueSnapshot == nextValue)
                    {
                        return nextValue;
                    }
                    await Task.Delay(100 * (retry + 1));
                }
                catch
                {
                    await Task.Delay(100 * (retry + 1));
                }
            }
            return 1;
        }

        public async Task<string> CreateTicketAsync(long chatId, string userMessage)
        {
            var allTickets = await GetAllTicketsAsync();
            if (allTickets.Any(t => t.ChatId == chatId))
            {
                return "❗ You already have a ticket. Please wait for staff to respond, or close your existing ticket.";
            }

            int ticketNumber = await GetNextTicketNumberAsync();
            string ticketId = $"TCK-{ticketNumber:D6}";

            var ticketData = new TicketData
            {
                TicketId = ticketId,
                Status = "Open",
                CreatedDate = DateTime.UtcNow,
                ChatId = chatId,
                Messages = new List<ChatMessage>()
            };

            if (!string.IsNullOrEmpty(userMessage))
            {
                var initialMessage = new ChatMessage(chatId.ToString(), "User", "Text") { TextContent = userMessage, Timestamp = DateTime.UtcNow };
                ticketData.Messages.Add(initialMessage);
            }

            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            await _firebaseClient.Child("openTickets").Child(ticketId).PutAsync(ticketData);
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase CreateTicketAsync PutAsync took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            return $"✅ Ticket created! Your Ticket ID is {ticketId}.";
        }

        public async Task AppendMessageToTicketAsync(string ticketId, ChatMessage message)
        {
            var openTicketRef = _firebaseClient.Child("openTickets").Child(ticketId);
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var existingOpenTicket = await openTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase AppendMessageToTicketAsync OnceSingleAsync (openTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            if (existingOpenTicket != null)
            {
                existingOpenTicket.Messages.Add(message);
                sw.Restart(); // RESTART TIMER
                await openTicketRef.PutAsync(existingOpenTicket);
                sw.Stop(); // STOP TIMER
                Console.WriteLine($"Firebase AppendMessageToTicketAsync PutAsync (openTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
                return;
            }

            var closedTicketRef = _firebaseClient.Child("closedTickets").Child(ticketId);
            sw.Restart(); // RESTART TIMER
            var existingClosedTicket = await closedTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase AppendMessageToTicketAsync OnceSingleAsync (closedTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            if (existingClosedTicket != null)
            {
                return;
            }
        }

        public async Task<TicketData?> GetTicketDataAsync(string ticketId)
        {
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var openTicketRef = _firebaseClient.Child("openTickets").Child(ticketId);
            var openTicketData = await openTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetTicketDataAsync OnceSingleAsync (openTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            if (openTicketData != null)
            {
                return openTicketData;
            }

            sw.Restart(); // RESTART TIMER
            var closedTicketRef = _firebaseClient.Child("closedTickets").Child(ticketId);
            var closedTicketData = await closedTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetTicketDataAsync OnceSingleAsync (closedTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            return closedTicketData;
        }

        public async Task<bool> IsTicketOpenAsync(string ticketId)
        {
            var ticketData = await GetTicketDataAsync(ticketId);
            return ticketData?.Status == "Open";
        }

        public async Task SetUserLanguageAsync(long userId, string languageCode)
        {
            // Serialize the languageCode to JSON format
            string languageCodeJson = JsonConvert.SerializeObject(languageCode);

            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            await _firebaseClient.Child("users").Child(userId.ToString()).Child("Language").PutAsync(languageCodeJson);
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase SetUserLanguageAsync PutAsync (users/{userId}/Language) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            // --- Update Cache on Set ---
            _userLanguageCache[userId] = languageCode; // Update cache when language is set
            // --- End Cache Update ---
        }

        public async Task<UserData?> GetUserDataAsync(long userId)
        {
            // --- Check Cache First ---
            if (_userLanguageCache.TryGetValue(userId, out string? cachedLanguage))
            {
                Console.WriteLine($"Debug: User language preference retrieved from cache for User ID: {userId}. Language: {cachedLanguage}");
                return new UserData { Language = cachedLanguage }; // Return cached data
            }
            // --- End Cache Check ---

            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            UserData? userData = await _firebaseClient.Child("users").Child(userId.ToString()).OnceSingleAsync<UserData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetUserDataAsync OnceSingleAsync (users/{userId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME


            // --- Populate Cache if data is from Firebase and has language ---
            if (userData?.Language != null)
            {
                _userLanguageCache[userId] = userData.Language; // Populate cache after Firebase fetch
                Console.WriteLine($"Debug: User language preference loaded from Firebase and cached for User ID: {userId}. Language: {userData.Language}");
            }
            // --- End Cache Population ---

            return userData;
        }

        private async Task LoadActiveTicketsFromFirebaseAsync()
        {
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var snapshot = await _firebaseClient.Child("openTickets").OnceAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase LoadActiveTicketsFromFirebaseAsync OnceAsync (openTickets) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            ActiveTicketsChatIdToTicketIdMap.Clear();
            if (snapshot.Any())
            {
                foreach (var child in snapshot)
                {
                    if (child.Object?.ChatId != null && child.Object?.TicketId != null)
                    {
                        ActiveTicketsChatIdToTicketIdMap.TryAdd(child.Object.ChatId, child.Object.TicketId);
                    }
                }
            }
        }

        public async Task<string> CloseTicketByTicketIdAsync(string ticketId)
        {
            var openTicketRef = _firebaseClient.Child("openTickets").Child(ticketId);
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var existingTicketData = await openTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase CloseTicketByTicketIdAsync OnceSingleAsync (openTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME


            if (existingTicketData != null)
            {
                existingTicketData.Status = "Closed";
                Stopwatch sw2 = Stopwatch.StartNew(); // START TIMER
                await _firebaseClient.Child("closedTickets").Child(ticketId).PutAsync(existingTicketData);
                await openTicketRef.DeleteAsync();
                sw2.Stop(); // STOP TIMER
                Console.WriteLine($"Firebase CloseTicketByTicketIdAsync PutAsync/DeleteAsync took: {sw2.ElapsedMilliseconds}ms"); // LOG TIME

                ActiveTicketsChatIdToTicketIdMap.TryRemove(existingTicketData.ChatId, out _);
                return $"✅ Ticket with Ticket ID: {ticketId} closed by staff.";
            }
            return "❗ No open ticket found with that Ticket ID.";
        }

        public async Task<List<TicketData>> GetOpenTicketsAsync()
        {
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var snapshot = await _firebaseClient.Child("openTickets").OnceAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetOpenTicketsAsync OnceAsync (openTickets) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            return snapshot.Select(childSnapshot => childSnapshot.Object).Where(ticket => ticket != null).ToList();
        }

        public async Task<List<TicketData>> GetClosedTicketsAsync()
        {
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var snapshot = await _firebaseClient.Child("closedTickets").OnceAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetClosedTicketsAsync OnceAsync (closedTickets) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            return snapshot.Select(childSnapshot => childSnapshot.Object).Where(ticket => ticket != null).ToList();
        }

        public async Task<List<TicketData>> GetAllTicketsAsync()
        {
            var openTickets = await GetOpenTicketsAsync();
            var closedTickets = await GetClosedTicketsAsync();
            return openTickets.Concat(closedTickets).ToList();
        }

        public async Task<List<ChatMessage>> GetTicketMessageHistoryAsync(string ticketId)
        {
            var openTicketRef = _firebaseClient.Child("openTickets").Child(ticketId);
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var existingOpenTicket = await openTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetTicketMessageHistoryAsync OnceSingleAsync (openTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            if (existingOpenTicket != null)
            {
                return existingOpenTicket.Messages;
            }

            var closedTicketRef = _firebaseClient.Child("closedTickets").Child(ticketId);
            sw.Restart(); // RESTART TIMER
            var existingClosedTicket = await closedTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase GetTicketMessageHistoryAsync OnceSingleAsync (closedTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            return existingClosedTicket?.Messages ?? new List<ChatMessage>();
        }

        public async Task<string> CloseTicketByChatIdAsync(long chatId)
        {
            string? ticketId = await GetActiveTicketIdForChatId(chatId);
            if (ticketId != null)
            {
                return await CloseTicketByTicketIdAsync(ticketId);
            }
            return "❗ You don't have an active ticket to close.";
        }

        private async Task<string?> GetActiveTicketIdForChatId(long chatId)
        {
            if (ActiveTicketsChatIdToTicketIdMap.TryGetValue(chatId, out string? ticketId))
            {
                return ticketId;
            }

            var openTickets = await GetOpenTicketsAsync();
            var userOpenTicket = openTickets.FirstOrDefault(t => t.ChatId == chatId);
            return userOpenTicket?.TicketId;
        }

        public async Task<string> DeleteTicketAsync(string ticketId)
        {
            var openTicketRef = _firebaseClient.Child("openTickets").Child(ticketId);
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var existingOpenTicket = await openTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase DeleteTicketAsync OnceSingleAsync (openTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            if (existingOpenTicket != null)
            {
                Stopwatch sw2 = Stopwatch.StartNew(); // START TIMER
                await openTicketRef.DeleteAsync();
                sw2.Stop(); // STOP TIMER
                Console.WriteLine($"Firebase DeleteTicketAsync DeleteAsync (openTickets/{ticketId}) took: {sw2.ElapsedMilliseconds}ms"); // LOG TIME

                ActiveTicketsChatIdToTicketIdMap.TryRemove(existingOpenTicket.ChatId, out _);
                return $"✅ Ticket with Ticket ID: {ticketId} has been permanently deleted.";
            }

            var closedTicketRef = _firebaseClient.Child("closedTickets").Child(ticketId);
            sw.Restart(); // RESTART TIMER
            var existingClosedTicket = await closedTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase DeleteTicketAsync OnceSingleAsync (closedTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
            if (existingClosedTicket != null)
            {
                sw.Restart(); // RESTART TIMER
                await closedTicketRef.DeleteAsync();
                sw.Stop(); // STOP TIMER
                Console.WriteLine($"Firebase DeleteTicketAsync DeleteAsync (closedTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME
                return $"✅ Ticket with Ticket ID: {ticketId} has been permanently deleted.";
            }
            return "❗ No ticket found with that Ticket ID to delete.";
        }

        public async Task<string> ReopenTicketAsync(string ticketId)
        {
            var closedTicketRef = _firebaseClient.Child("closedTickets").Child(ticketId);
            Stopwatch sw = Stopwatch.StartNew(); // START TIMER
            var existingTicketData = await closedTicketRef.OnceSingleAsync<TicketData>();
            sw.Stop(); // STOP TIMER
            Console.WriteLine($"Firebase ReopenTicketAsync OnceSingleAsync (closedTickets/{ticketId}) took: {sw.ElapsedMilliseconds}ms"); // LOG TIME

            if (existingTicketData != null)
            {
                existingTicketData.Status = "Open";
                Stopwatch sw2 = Stopwatch.StartNew(); // START TIMER
                await _firebaseClient.Child("openTickets").Child(ticketId).PutAsync(existingTicketData);
                await closedTicketRef.DeleteAsync();
                sw2.Stop(); // STOP TIMER
                Console.WriteLine($"Firebase ReopenTicketAsync PutAsync/DeleteAsync took: {sw2.ElapsedMilliseconds}ms"); // LOG TIME

                ActiveTicketsChatIdToTicketIdMap.TryAdd(existingTicketData.ChatId, ticketId);
                return $"✅ Ticket with Ticket ID: {ticketId} reopened.";
            }
            return "❗ No closed ticket found with that Ticket ID.";
        }

        public async Task<List<TicketData>> SearchTicketsAsync(string keyword)
        {
            var allTickets = await GetAllTicketsAsync();
            string keywordLower = keyword.ToLowerInvariant();

            return allTickets.Where(ticket =>
                ticket.TicketId?.ToLowerInvariant().Contains(keywordLower) == true ||
                ticket.Messages.Any(message => message.MessageType == "Text" && message.TextContent?.ToLowerInvariant().Contains(keywordLower) == true)
            ).ToList();
        }

        public (bool IsHandling, long StaffChatId) IsTicketBeingHandledAsync(string ticketId)
        {
            if (Program.staffBot != null)
            {
                return Program.staffBot.IsHandlingTicket(ticketId); // Delegates to StaffBot.IsHandlingTicket
            }
            return (false, 0);
        }
    }
}