namespace DataLoaders.Exstensions;

public static class RomajiExstension
{
    public static string ToRomaji(this string kana) => 
        WanaKanaSharp.WanaKana.ToRomaji(kana).Replace("'", "").Replace("~", "");

}