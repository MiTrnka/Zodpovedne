// Zodpovedne.Data/Models/FcmRegistrationToken.cs

using System;
using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Reprezentuje záznam o registračním tokenu zařízení pro Firebase Cloud Messaging (FCM).
/// Každý záznam v této tabulce je unikátní adresa pro doručení notifikace.
/// </summary>
public class FcmRegistrationToken
{
    /// <summary>
    /// Primární klíč tabulky. Unikátní identifikátor pro každý záznam.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Samotný registrační token, který aplikace získá od Firebase SDK.
    /// Tento token se může časem měnit (např. při přeinstalaci aplikace),
    /// proto je důležité ho udržovat aktuální.
    /// Označujeme ho jako povinný ([Required]), protože bez něj záznam nedává smysl.
    /// </summary>
    [Required]
    public string Token { get; set; }

    /// <summary>
    /// Časové razítko, kdy byl tento token vytvořen nebo naposledy aktualizován v naší databázi.
    /// Pomáhá nám to například při čištění starých, již neplatných tokenů.
    /// </summary>
    [Required]
    public DateTime CreatedUtc { get; set; }
}