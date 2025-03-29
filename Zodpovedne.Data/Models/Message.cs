// Zodpovedne.Data/Models/Message.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Zpráva mezi uživateli.
/// </summary>
public class Message
{
    public int Id { get; set; }

    [Required]
    public string SenderUserId { get; set; } = "";

    [Required]
    public string RecipientUserId { get; set; } = "";

    [Required(ErrorMessage = "Text zprávy je povinný.")]
    [MaxLength(1000, ErrorMessage = "Zpráva může mít maximálně 1000 znaků.")]
    public string Content { get; set; } = "";

    /// <summary>
    /// Čas, kdy byla zpráva odeslána.
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Čas, kdy byla zpráva přečtena.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Typ zprávy (normální, smazaná...).
    /// </summary>
    [Required]
    public MessageType MessageType { get; set; } = MessageType.Normal;

    // Navigační vlastnosti
    [ForeignKey("SenderUserId")]
    public virtual ApplicationUser SenderUser { get; set; } = null!;

    [ForeignKey("RecipientUserId")]
    public virtual ApplicationUser RecipientUser { get; set; } = null!;
}