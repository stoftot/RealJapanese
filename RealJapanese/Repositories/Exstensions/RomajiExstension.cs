using System.Text.RegularExpressions;

namespace Repositories.Exstensions;

public static class RomajiExstension
{
    public static string ToRomaji(this string kana) => 
        Regex.Replace(
            WanaKanaSharp.WanaKana
                .ToRomaji(kana)
                .Replace("'", "")
                .Replace("~", ""),
            "([aeiou])-", "$1$1"
            );

}