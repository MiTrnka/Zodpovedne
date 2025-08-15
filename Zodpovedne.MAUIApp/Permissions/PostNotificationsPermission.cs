#if ANDROID
using Microsoft.Maui.ApplicationModel;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace Zodpovedne.MAUIApp.Permissions;

/// <summary>
/// Vlastní definice oprávnění pro zasílání notifikací na Androidu 13 a vyšším.
/// .NET MAUI vyžaduje tuto třídu, aby věděl, jak o systémové oprávnění požádat.
/// Tato třída definuje vlastní oprávnění pro zasílání notifikací, které je vyžadováno na Androidu 13 (API 33) a vyšším.
/// Funguje jako nezbytný most mezi deklarací oprávnění v AndroidManifest.xml a C# kódem aplikace.
/// Umožňuje multiplatformní metodě Permissions.RequestAsync<T>() zjistit, jaké konkrétní nativní
/// oprávnění má v systému Android vyžádat, a následně zobrazit uživateli příslušný dialog.
/// </summary>
public class PostNotificationsPermission : BasePlatformPermission
{
    // Zde definujeme, která oprávnění z Android Manifestu tato třída reprezentuje.
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new List<(string androidPermission, bool isRuntime)>
        {
            // Přesný název oprávnění a flag, že se o něj žádá za běhu aplikace.
            (global::Android.Manifest.Permission.PostNotifications, true)
        }.ToArray();
}
#endif