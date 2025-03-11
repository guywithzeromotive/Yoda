using System;
using System.IO;
using FirebaseAdmin;
using Firebase.Database;
using Google.Apis.Auth.OAuth2;
using Yoda_Bot.Utils;
using Yoda_Bot.Config;

namespace Yoda_Bot.Firebase
{
    public static class FirebaseInitializer
    {
        private static FirebaseClient? firebase;
        private static FirebaseApp? firebaseApp;

        public static FirebaseClient InitializeFirebase(AppConfig appConfig)
        {
            string firebaseDatabaseUrl = appConfig.FirebaseDatabaseUrl
                                       ?? throw new ConfigurationException("Firebase Database URL is missing in AppConfig.");
            string serviceAccountKeyPath = appConfig.FirebaseServiceAccountKeyPath
                                         ?? throw new ConfigurationException("Firebase service account key path is missing in AppConfig.");

            if (!File.Exists(serviceAccountKeyPath))
            {
                throw new ConfigurationException($"Firebase service account key file not found at: '{serviceAccountKeyPath}'.");
            }

            try
            {
                firebaseApp = FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(serviceAccountKeyPath)
                });
                Console.WriteLine("Firebase App initialized successfully.");

                firebase = new FirebaseClient(firebaseDatabaseUrl);
                Console.WriteLine("Firebase Database initialized successfully.");
                return firebase;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Firebase: {ex.Message}");
                throw new ConfigurationException("Failed to initialize Firebase.", ex); // Throw exception
            }
        }

        /*public static FirebaseClient? GetFirebaseClient() // Keep if you need it, though direct return from InitializeFirebase is usually enough
        {
            return firebase;
        }*/
    }
}