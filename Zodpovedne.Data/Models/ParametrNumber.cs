using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Entita reprezentující historii přihlášení uživatelů
/// </summary>
public class ParametrNumber
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ParametrName { get; set; } = "";

    public long ParametrValue { get; set; }
}