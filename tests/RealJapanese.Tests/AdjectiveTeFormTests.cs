using DataLoaders.Models;

namespace RealJapanese.Tests;

/// <summary>Checks adjective te forms, including the single-character na practice entry.</summary>
public sealed class AdjectiveTeFormTests
{
    [Theory]
    [InlineData("高い", "たかい", "i", "高くて", "たかくて")]
    [InlineData("静か", "しずか", "na", "静かで", "しずかで")]
    [InlineData("きれい", "きれい", "na", "きれいで", "きれいで")]
    [InlineData("いい", "いい", "irregular", "よくて", "よくて")]
    [InlineData("かっこいい", "かっこいい", "irregular", "かっこよくて", "かっこよくて")]
    [InlineData("い", "い", "i", "くて", "くて")]
    [InlineData("な", "な", "na", "なで", "なで")]
    public void TeForm_UsesTheEndingForItsAdjectiveType(
        string japanese, string kana, string type, string japaneseExpected, string kanaExpected)
    {
        var adjective = new Adjective { Japanese = japanese, Kana = kana, Type = type, English = "test" };

        Assert.Equal(japaneseExpected, adjective.TeForm(Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(kanaExpected, adjective.TeForm(Conjugatabel.ToConjugate.Kana));
    }
}
