#if ANDROID
using Microsoft.Maui.ApplicationModel;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace Zodpovedne.MAUIApp.Permissions;

/// <summary>
/// Vlastní definice oprávnění pro zasílání notifikací na Androidu 13 a vyšším.
/// .NET MAUI vyžaduje tuto třídu, aby věděl, jak o systémové oprávnění požádat.
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