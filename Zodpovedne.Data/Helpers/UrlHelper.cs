using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Zodpovedne.Data.Helpers;

public static class UrlHelper
{
    public static string GenerateUrlFriendlyCode(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Převedeme na malá písmena a odstraníme diakritiku
        text = text.ToLower();
        var bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(text);
        text = System.Text.Encoding.ASCII.GetString(bytes);

        // Nahradíme mezery podtržítkem a odstraníme nepovolené znaky
        text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
        text = Regex.Replace(text, @"\s+", "_");

        // Omezíme délku na 150 znaků (necháme prostor pro suffix)
        if (text.Length > 150)
            text = text.Substring(0, 150);

        return text;
    }

    public static string GenerateUniqueSuffix()
    {
        // Použijeme jen písmena a číslice, které jsou bezpečné pro URL
        const string chars = "abcdefghijkmnpqrstuvwxyz23456789"; // vynecháváme podobně vypadající znaky jako 'l', 'o', '0', '1'
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)])
            .ToArray());
    }
}