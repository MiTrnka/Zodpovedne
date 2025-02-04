using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Zodpovedne.Data.Helpers;

public static class UrlHelper
{
    public static string GenerateUrlFriendlyCode(string text)
    {
        if (string.IsNullOrEmpty(text)) return "discussion";

        var replacements = new Dictionary<string, string>
           {
               {"á", "a"}, {"č", "c"}, {"ď", "d"}, {"é", "e"}, {"ě", "e"},
               {"í", "i"}, {"ň", "n"}, {"ó", "o"}, {"ř", "r"}, {"š", "s"},
               {"ť", "t"}, {"ú", "u"}, {"ů", "u"}, {"ý", "y"}, {"ž", "z"},
               {" ", "_"}, {"-", "_"}
           };

        text = text.ToLower();
        foreach (var replacement in replacements)
        {
            text = text.Replace(replacement.Key, replacement.Value);
        }

        // Ponechání pouze povolených znaků (a-z, 0-9 a _)
        text = Regex.Replace(text, @"[^a-z0-9_]", "");

        // Nahrazení více podtržítek jedním
        text = Regex.Replace(text, @"_+", "_");

        // Odstranění podtržítek na začátku a konci
        text = text.Trim('_');

        // Omezení délky
        if (text.Length > 150)
            text = text.Substring(0, 150).TrimEnd('_');

        return string.IsNullOrEmpty(text) ? "discussion" : text;
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