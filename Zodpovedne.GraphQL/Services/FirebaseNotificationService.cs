using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;

namespace Zodpovedne.GraphQL.Services;

public class FirebaseNotificationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private static bool _firebaseAppInitialized = false;

    public FirebaseNotificationService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        InitializeFirebaseApp();
    }

    private static void InitializeFirebaseApp()
    {
        // Zajistíme, aby se Firebase App inicializovala pouze jednou za životní cyklus aplikace
        if (_firebaseAppInitialized) return;

        var credential = GoogleCredential.FromFile("firebase-credentials.json");
        FirebaseApp.Create(new AppOptions { Credential = credential });
        _firebaseAppInitialized = true;
    }

    /// <summary>
    /// Rozešle notifikaci na všechna zařízení registrovaná v databázi.
    /// </summary>
    public async Task<string> SendGlobalNotificationAsync(string title, string body)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tokens = await context.FcmRegistrationTokens.Select(t => t.Token).ToListAsync();

        if (tokens.Count == 0)
        {
            return "No devices registered to send notification to.";
        }

        var message = new MulticastMessage()
        {
            Tokens = tokens,
            Notification = new Notification
            {
                Title = title,
                Body = body,
            },
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
        return $"Successfully sent message to {response.SuccessCount} devices. Failed for {response.FailureCount} devices.";
    }
}