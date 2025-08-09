using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Zodpovedne.Data.Models;

public class FreeMessage
{
    // Primární klíč, databáze mu automaticky přiřadí hodnotu
    public int Id { get; set; }

    // Datum a čas vytvoření
    public DateTime CreatedUtc { get; set; }

    // Jméno uživatele, maximálně 50 znaků
    [Required]
    [StringLength(50)]
    public string Nickname { get; set; } = string.Empty;

    // Text zprávy, bez omezení délky
    [Required]
    public string Text { get; set; } = string.Empty;
}
