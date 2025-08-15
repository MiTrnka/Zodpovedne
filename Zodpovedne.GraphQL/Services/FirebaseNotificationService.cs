using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;

namespace Zodpovedne.GraphQL.Services;

/// <summary>
/// Zajišťuje veškerou komunikaci se službou Firebase Cloud Messaging (FCM) ze strany serveru.
/// Tato třída je zodpovědná za jednorázovou inicializaci Firebase Admin SDK
/// a za rozesílání push notifikací na registrovaná klientská zařízení.
/// </summary>
public class FirebaseNotificationService
{
    /// <summary>
    /// Továrna pro vytváření instancí databázového kontextu. Používá se pro bezpečný
    /// přístup k databázi v rámci asynchronních operací.
    /// </summary>
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    /// <summary>
    /// Statický příznak, který zajišťuje, že se Firebase Admin SDK inicializuje
    /// pouze jednou během celého životního cyklu aplikace, což je doporučený postup.
    /// </summary>
    private static bool _firebaseAppInitialized = false;

    /// <summary>
    /// Konstruktor služby, který je volán při dependency injection.
    /// Přijímá továrnu na DbContext a spouští inicializaci Firebase.
    /// </summary>
    public FirebaseNotificationService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        InitializeFirebaseApp();
    }

    /// <summary>
    /// Statická metoda, která provádí jednorázovou inicializaci Firebase Admin SDK.
    /// Načte přihlašovací údaje ze souboru 'firebase-credentials.json' a nakonfiguruje
    /// výchozí instanci FirebaseApp.
    /// </summary>
    private static void InitializeFirebaseApp()
    {
        if (_firebaseAppInitialized) return;

        var credential = GoogleCredential.FromFile("firebase-credentials.json");
        FirebaseApp.Create(new AppOptions { Credential = credential });
        _firebaseAppInitialized = true;
    }

    /// <summary>
    /// Asynchronně odešle zadanou notifikaci na všechna zařízení,
    /// jejichž registrační FCM tokeny jsou uloženy v databázi.
    /// </summary>
    /// <param name="title">Titulek notifikace, který se zobrazí uživateli.</param>
    /// <param name="body">Hlavní text (tělo) notifikace.</param>
    /// <returns>Řetězec se souhrnem o počtu úspěšně a neúspěšně odeslaných zpráv.</returns>
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