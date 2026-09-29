namespace Repositories.Kana;

public enum KanaScript
{
    Hiragana,
    Katakana
}

public enum KanaGroup
{
    Single,
    Double,
    Extended
}

public sealed record KanaCharacter(
    string Id,
    KanaScript Script,
    KanaGroup Group,
    string Text,
    string Romaji,
    int Column,
    int Row,
    string[] Aliases)
{
    public bool Accepts(string answer)
    {
        var normalized = answer.Trim();
        if (normalized.Length == 0 || normalized.Any(character =>
                character is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z')))
        {
            return false;
        }

        normalized = normalized.ToLowerInvariant();
        return string.Equals(normalized, Romaji, StringComparison.Ordinal) ||
               Aliases.Contains(normalized, StringComparer.Ordinal);
    }
}

public static class KanaCatalog
{
    private static readonly (string Text, string Romaji, string[] Aliases)[][] SingleRows =
    [
        [ ("あ", "a", []), ("か", "ka", []), ("さ", "sa", []), ("た", "ta", []), ("な", "na", []), ("は", "ha", []), ("ま", "ma", []), ("や", "ya", []), ("ら", "ra", []), ("わ", "wa", []), ("", "", []), ("が", "ga", []), ("ざ", "za", []), ("だ", "da", []), ("ば", "ba", []), ("ぱ", "pa", []) ],
        [ ("い", "i", []), ("き", "ki", []), ("し", "shi", ["si"]), ("ち", "chi", ["ti"]), ("に", "ni", []), ("ひ", "hi", []), ("み", "mi", []), ("", "", []), ("り", "ri", []), ("", "", []), ("", "", []), ("ぎ", "gi", []), ("じ", "ji", ["zi"]), ("ぢ", "ji", ["di"]), ("び", "bi", []), ("ぴ", "pi", []) ],
        [ ("う", "u", []), ("く", "ku", []), ("す", "su", []), ("つ", "tsu", ["tu"]), ("ぬ", "nu", []), ("ふ", "fu", ["hu"]), ("む", "mu", []), ("ゆ", "yu", []), ("る", "ru", []), ("", "", []), ("", "", []), ("ぐ", "gu", []), ("ず", "zu", []), ("づ", "zu", ["du"]), ("ぶ", "bu", []), ("ぷ", "pu", []) ],
        [ ("え", "e", []), ("け", "ke", []), ("せ", "se", []), ("て", "te", []), ("ね", "ne", []), ("へ", "he", []), ("め", "me", []), ("", "", []), ("れ", "re", []), ("", "", []), ("", "", []), ("げ", "ge", []), ("ぜ", "ze", []), ("で", "de", []), ("べ", "be", []), ("ぺ", "pe", []) ],
        [ ("お", "o", []), ("こ", "ko", []), ("そ", "so", []), ("と", "to", []), ("の", "no", []), ("ほ", "ho", []), ("も", "mo", []), ("よ", "yo", []), ("ろ", "ro", []), ("を", "wo", ["o"]), ("ん", "n", ["nn"]), ("ご", "go", []), ("ぞ", "zo", []), ("ど", "do", []), ("ぼ", "bo", []), ("ぽ", "po", []) ]
    ];

    private static readonly (string Column, string[] Text, string[] Romaji, string[][] Aliases)[] DoubleColumns =
    [
        ("ky", ["きゃ", "きゅ", "きょ"], ["kya", "kyu", "kyo"], [[], [], []]),
        ("sh", ["しゃ", "しゅ", "しょ"], ["sha", "shu", "sho"], [["sya"], ["syu"], ["syo"]]),
        ("ch", ["ちゃ", "ちゅ", "ちょ"], ["cha", "chu", "cho"], [["tya"], ["tyu"], ["tyo"]]),
        ("ny", ["にゃ", "にゅ", "にょ"], ["nya", "nyu", "nyo"], [[], [], []]),
        ("hy", ["ひゃ", "ひゅ", "ひょ"], ["hya", "hyu", "hyo"], [[], [], []]),
        ("my", ["みゃ", "みゅ", "みょ"], ["mya", "myu", "myo"], [[], [], []]),
        ("ry", ["りゃ", "りゅ", "りょ"], ["rya", "ryu", "ryo"], [[], [], []]),
        ("gy", ["ぎゃ", "ぎゅ", "ぎょ"], ["gya", "gyu", "gyo"], [[], [], []]),
        ("j", ["じゃ", "じゅ", "じょ"], ["ja", "ju", "jo"], [["jya"], ["jyu"], ["jyo"]]),
        ("dy", ["ぢゃ", "ぢゅ", "ぢょ"], ["ja", "ju", "jo"], [["dya"], ["dyu"], ["dyo"]]),
        ("by", ["びゃ", "びゅ", "びょ"], ["bya", "byu", "byo"], [[], [], []]),
        ("py", ["ぴゃ", "ぴゅ", "ぴょ"], ["pya", "pyu", "pyo"], [[], [], []])
    ];

    private static readonly (string Column, string[] Text, string[] Romaji)[] ExtendedColumns =
    [
        ("ye", ["イェ"], ["ye"]),
        ("w", ["ウィ", "ウェ", "ウォ"], ["wi", "we", "wo"]),
        ("v", ["ヴァ", "ヴィ", "ヴ", "ヴェ", "ヴォ"], ["va", "vi", "vu", "ve", "vo"]),
        ("sh", ["シェ"], ["she"]),
        ("j", ["ジェ"], ["je"]),
        ("ch", ["チェ"], ["che"]),
        ("t", ["ティ", "トゥ"], ["ti", "tu"]),
        ("d", ["ディ", "ドゥ"], ["di", "du"]),
        ("ts", ["ツァ", "ツィ", "ツェ", "ツォ"], ["tsa", "tsi", "tse", "tso"]),
        ("f", ["ファ", "フィ", "フェ", "フォ"], ["fa", "fi", "fe", "fo"]),
        ("vy", ["ヴャ", "ヴュ", "ヴョ"], ["vya", "vyu", "vyo"]),
        ("ty", ["テュ"], ["tyu"]),
        ("dy", ["デュ"], ["dyu"]),
        ("fy", ["フュ"], ["fyu"])
    ];

    private static readonly IReadOnlyList<KanaCharacter> Catalog = Array.AsReadOnly(BuildCatalog());

    public static IReadOnlyList<KanaCharacter> Characters => Catalog;

    private static KanaCharacter[] BuildCatalog()
    {
        var characters = new List<KanaCharacter>(244);
        foreach (var script in Enum.GetValues<KanaScript>())
        {
            var scriptPrefix = script == KanaScript.Hiragana ? "h-" : "k-";
            var hiragana = script == KanaScript.Hiragana;
            for (var row = 0; row < SingleRows.Length; row++)
            {
                for (var column = 0; column < SingleRows[row].Length; column++)
                {
                    var (text, romaji, aliases) = SingleRows[row][column];
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (!hiragana)
                    {
                        text = ToKatakana(text);
                    }

                    characters.Add(new KanaCharacter(scriptPrefix + text, script, KanaGroup.Single,
                        text, romaji, column, row, aliases));
                }
            }

            for (var column = 0; column < DoubleColumns.Length; column++)
            {
                var (_, text, romaji, aliases) = DoubleColumns[column];
                for (var row = 0; row < text.Length; row++)
                {
                    var kana = hiragana ? text[row] : ToKatakana(text[row]);
                    characters.Add(new KanaCharacter(scriptPrefix + kana, script, KanaGroup.Double,
                        kana, romaji[row], column, row, aliases[row]));
                }
            }
        }

        for (var column = 0; column < ExtendedColumns.Length; column++)
        {
            var (_, text, romaji) = ExtendedColumns[column];
            for (var index = 0; index < text.Length; index++)
            {
                var row = VowelRow(romaji[index]);
                characters.Add(new KanaCharacter("k-" + text[index], KanaScript.Katakana, KanaGroup.Extended,
                    text[index], romaji[index], column, row, []));
            }
        }

        return characters.ToArray();
    }

    private static string ToKatakana(string hiragana) =>
        string.Concat(hiragana.Select(character => character is >= '\u3041' and <= '\u3096'
            ? (char)(character + 0x60)
            : character));

    private static int VowelRow(string romaji) => romaji[^1] switch
    {
        'a' => 0,
        'i' => 1,
        'u' => 2,
        'e' => 3,
        'o' => 4,
        _ => throw new InvalidOperationException($"Extended kana reading '{romaji}' has no vowel row.")
    };
}
