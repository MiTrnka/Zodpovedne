namespace Zodpovedne.Contracts.Enums;

/// <summary>
/// Určuje typ hlasování v diskuzi
/// </summary>
public enum VoteType
{
    /// <summary>
    /// Diskuze nemá hlasování
    /// </summary>
    None = 0,

    /// <summary>
    /// Diskuze má hlasování, které je viditelné uživatelům a lze v něm hlasovat
    /// </summary>
    Visible = 1,

    /// <summary>
    /// Diskuze má hlasování, které je viditelné uživatelům, ale již nelze hlasovat - pouze zobrazení výsledků
    /// </summary>
    Closed = 2,

    /// <summary>
    /// Diskuze má hlasování, ale je skryté pro běžné uživatele (vidí ho pouze autor a admin)
    /// </summary>
    Hidden = 3
}