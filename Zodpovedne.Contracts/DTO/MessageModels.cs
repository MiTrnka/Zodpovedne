using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Contracts.DTO;

/// <summary>
/// DTO pro reprezentaci konverzace s jedním uživatelem
/// </summary>
public class ConversationDto
{
    /// <summary>
    /// ID uživatele, se kterým probíhá konverzace
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// Přezdívka uživatele, se kterým probíhá konverzace
    /// </summary>
    public string UserNickname { get; set; } = "";

    /// <summary>
    /// Čas poslední zprávy v konverzaci
    /// </summary>
    public DateTime LastMessageTime { get; set; }

    /// <summary>
    /// Náhled obsahu poslední zprávy (začátek zprávy)
    /// </summary>
    public string LastMessagePreview { get; set; } = "";

    /// <summary>
    /// Určuje, zda poslední zprávu poslal přihlášený uživatel (true) nebo druhá strana (false)
    /// </summary>
    public bool IsLastMessageFromCurrentUser { get; set; }

    /// <summary>
    /// Počet nepřečtených zpráv v této konverzaci
    /// </summary>
    public int UnreadCount { get; set; }
}

/// <summary>
/// DTO pro reprezentaci jedné zprávy v konverzaci
/// </summary>
public class MessageDto
{
    /// <summary>
    /// ID zprávy
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID odesílatele zprávy
    /// </summary>
    public string SenderUserId { get; set; } = "";

    /// <summary>
    /// Přezdívka odesílatele zprávy
    /// </summary>
    public string SenderNickname { get; set; } = "";

    /// <summary>
    /// ID příjemce zprávy
    /// </summary>
    public string RecipientUserId { get; set; } = "";

    /// <summary>
    /// Přezdívka příjemce zprávy
    /// </summary>
    public string RecipientNickname { get; set; } = "";

    /// <summary>
    /// Obsah zprávy
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Čas odeslání zprávy
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Čas přečtení zprávy (null pokud ještě nebyla přečtena)
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Typ zprávy (normální, smazaná, ...)
    /// </summary>
    public MessageType MessageType { get; set; }
}

/// <summary>
/// DTO pro odeslání nové zprávy
/// </summary>
public class SendMessageDto
{
    /// <summary>
    /// ID příjemce zprávy
    /// </summary>
    public string RecipientUserId { get; set; } = "";

    /// <summary>
    /// Obsah zprávy
    /// </summary>
    public string Content { get; set; } = "";
}

/// <summary>
/// DTO pro paginaci zpráv v konverzaci
/// </summary>
public class MessagesPaginationDto
{
    /// <summary>
    /// Seznam zpráv na aktuální stránce
    /// </summary>
    public List<MessageDto> Messages { get; set; } = new();

    /// <summary>
    /// Indikátor, zda existují starší zprávy, které lze načíst
    /// </summary>
    public bool HasOlderMessages { get; set; }

    /// <summary>
    /// Celkový počet zpráv v konverzaci
    /// </summary>
    public int TotalCount { get; set; }
}
