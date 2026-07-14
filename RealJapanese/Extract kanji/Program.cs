using System.Globalization;
using System.Text;
using DataLoaders;
using DataLoaders.Models;
using Extract_kanji;
using WanaKanaSharp;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine(WanaKana.IsKanji("新"));
const string modelsDirectory = @"E:\AI\Models";
Console.WriteLine("Locating models folder...");
if (!Directory.Exists(modelsDirectory))
{
    Console.WriteLine("Directory doesn't exist");
    return 1;
}
Console.WriteLine("Found models folder");
Console.WriteLine("Loading models...");
await using var ai = new Ai(modelsDirectory);
Console.WriteLine("Finished");

Console.WriteLine("Loading existing data...");
const string baseDataPath = "../../../../Data/";
var data = new Dictionary<DataType, List<MinimumWordData>>()
{
    {
        DataType.Verbs,
        new JsonLoader<Verb>(folderPath: baseDataPath + "Verbs", fileName: "Verbs.json")
            .Load()
            .Select(v => new MinimumWordData(v.Id, v.Japanese))
            .ToList()
    },
    {
        DataType.Adjective,
        new JsonLoader<Adjective>(folderPath: baseDataPath + "Adjectives", fileName: "Adjectives.json")
            .Load()
            .Select(a => new MinimumWordData(a.Id, a.Japanese))
            .ToList()
    },
    {
        DataType.Word,
        new JsonLoader<Word>(folderPath: baseDataPath + "Words", fileName: "Words.json")
            .Load()
            .Select(w => new MinimumWordData(w.Id, w.Japanese))
            .ToList()
    }
};

var kanjiDataBasePath = $"{baseDataPath}/Kanji/FromOtherData";
var currentKanjiData = new JsonLoader<Kanji>(folderPath: kanjiDataBasePath, fileName: "kanji.json").Load().OrderBy(k => k.Id).ToList();
var currentKanjiDataSaver =  new JsonSaver<Kanji>(folderPath: kanjiDataBasePath, fileName: "kanji.json");

var kanjiToIdDict = currentKanjiData.ToDictionary(k => k.Symbol, k => k.Id);

var kanjiRelationData = new JsonLoader<KanjiRelationData>(folderPath: kanjiDataBasePath, fileName: "kanjiRelationData.json").Load()
    .ToDictionary(k => k.KanjiId, k => k);
var kanjiRelationDataSaver = new JsonSaver<List<KanjiRelationData>>(folderPath: kanjiDataBasePath, fileName: "kanjiRelationData.json");

var nextKanjiId = currentKanjiData.Count == 0 ? 0 : currentKanjiData.Last().Id+1;
var NewKanjis = new List<NewKanji>();
Console.WriteLine("Finished");

//extract all the kanji from the different words and register them in a map for kanji id to word id
Console.WriteLine("Extracting kanjis from data...");
foreach (var d in data)
{
    foreach (var value in d.Value)
    {
        var kanjiPresent = SplitJapaneseIntoCharacters(value.WordInJapanese).Where(WanaKana.IsKanji).ToList();
        foreach (var kanji in kanjiPresent)
        {
            ProcessWord(kanji, value.Id, d.Key);
        }
    }
}
Console.WriteLine("Finished");

//fill out the kanji

if (NewKanjis.Count == 0)
{
    Console.WriteLine("No new kanjis found");
}
else
{
    Console.WriteLine(NewKanjis.Count + " new kanjis found");
}

Console.WriteLine("Getting kanji data...");

var completed = 0;
var total = NewKanjis.Count;

var tasks = NewKanjis.Select(async newKanji =>
{
    Kanji kanji = await ai.FillOutForKanji(newKanji);

    int current = Interlocked.Increment(ref completed);
    Console.WriteLine($"Done {current}/{total}");

    return kanji;
});

Kanji[] results = await Task.WhenAll(tasks);

currentKanjiData.AddRange(results.OrderBy(k => k.Id));

Console.WriteLine("Finished");

//save it
Console.WriteLine("Staring saving...");
currentKanjiDataSaver.Save(currentKanjiData);
kanjiRelationDataSaver.Save(kanjiRelationData.Values.ToList());
Console.WriteLine("Finished");
Console.WriteLine("Done");
return 0;


void ProcessWord(string kanji, int valueId, DataType category)
{
    if (kanjiToIdDict.TryGetValue(kanji, out var kanjiId))
    {
        PutIntoCorrectCategory(kanjiId, valueId, category);
    }
    else
    {
        kanjiToIdDict.Add(kanji, nextKanjiId);
        kanjiRelationData.Add(nextKanjiId, new KanjiRelationData()
        {
            KanjiId = nextKanjiId,
            AdjectiveIds = [],
            VerbIds = [],
            WordIds = []
        });
        PutIntoCorrectCategory(nextKanjiId, valueId, category);
        NewKanjis.Add(new NewKanji()
        {
            Kanji = kanji,
            KanjiId = nextKanjiId
        });
        nextKanjiId++;
    }
}

void PutIntoCorrectCategory (int kanjiId, int valueId, DataType category)
{
    switch (category)
    {
        case DataType.Verbs:
            kanjiRelationData[kanjiId].VerbIds.Add(valueId);
            break;
        case DataType.Adjective:
            kanjiRelationData[kanjiId].AdjectiveIds.Add(valueId);
            break;
        case DataType.Word:
            kanjiRelationData[kanjiId].WordIds.Add(valueId);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(category), category, null);
    }
}

string[] SplitJapaneseIntoCharacters(string japanese)
{
    var elements = new List<string>();
    
    TextElementEnumerator enumerator =
        StringInfo.GetTextElementEnumerator(japanese);

    while (enumerator.MoveNext())
    {
        elements.Add(enumerator.GetTextElement());
    }

    return elements.ToArray();
}

internal enum DataType
{
    Verbs,
    Adjective,
    Word
}

public record NewKanji()
{
    public int KanjiId { get; init; }
    public string Kanji { get; init; }
}

internal record MinimumWordData(int Id, string WordInJapanese);