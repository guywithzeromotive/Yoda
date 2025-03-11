// Utils/LanguageParser.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic; // Import for Dictionary

namespace Yoda_Bot.Utils
{
    public class LanguageParser
    {
        private Dictionary<string, string> _messages;  // Use a dictionary for efficient lookup

        // Public Constructor (Reverted to original for now)
        public LanguageParser(string languageCode)
        {
            _messages = LoadLanguageFile(languageCode).GetAwaiter().GetResult(); // Keep synchronous load for constructor
            if (_messages == null)
            {
                // Handle the error.  Maybe throw an exception, or log and use a default language.
                Console.WriteLine($"Error: Failed to load language file for '{languageCode}'.");
                _messages = new Dictionary<string, string>(); // Use an empty dictionary to prevent NullReferenceExceptions later
            }
        }

        private async Task<Dictionary<string, string>> LoadLanguageFile(string languageCode) // Keep this private and async
        {
            string filePath = $"./languages/{languageCode}.json";
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: Language file '{filePath}' not found.");
                return new Dictionary<string, string>();
            }
            try
            {
                string jsonString = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };//for case insensetive
                return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString, options) ?? new Dictionary<string, string>(); // Return empty dictionary if null

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading language file '{filePath}': {ex}");
                return new Dictionary<string, string>(); // Return empty dictionary instead of null
            }
        }


        public string GetMessage(string key)
        {
            if (_messages.TryGetValue(key, out string? message))
            {
                return message;
            }
            else
            {
                Console.WriteLine($"Warning: Missing translation for key '{key}'."); // Log missing keys
                return $"[{key}]"; // Return the key itself as a fallback, so it's obvious in the UI.
            }
        }
    }
}