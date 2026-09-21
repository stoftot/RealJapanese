using DataLoaders.Models;
using Repositories;
using Repositories.DTOs;
using Repositories.Exstensions;

internal static class StudyLogicChecks
{
    public static void Run()
    {
        VerifyChunks();
        VerifyShuffle();
        VerifyPracticeCategories();
        VerifyQuestionConversions();
        VerifyRomajiNormalization();
        VerifyVerbConjugations();
        VerifyVerbTeForms();
        VerifyAdjectiveConjugations();
        VerifyCounting();
        VerifyTimes();
        VerifyKanjiNumbers();
    }

    private static void VerifyChunks()
    {
        var values = Enumerable.Range(0, 10).ToArray();
        SequenceEqual([0, 1, 2, 3], values.GetChunk(3, 0), "GetChunk(10 items, 3 chunks, index 0)");
        SequenceEqual([4, 5, 6], values.GetChunk(3, 1), "GetChunk(10 items, 3 chunks, index 1)");
        SequenceEqual([7, 8, 9], values.GetChunk(3, 2), "GetChunk(10 items, 3 chunks, index 2)");
        SequenceEqual(Array.Empty<int>(), Array.Empty<int>().GetChunk(3, 1), "GetChunk(empty source)");
        SequenceEqual([0], new[] { 0, 1 }.GetChunk(4, 0), "GetChunk(more chunks than items, index 0)");
        SequenceEqual([1], new[] { 0, 1 }.GetChunk(4, 1), "GetChunk(more chunks than items, index 1)");
        SequenceEqual(Array.Empty<int>(), new[] { 0, 1 }.GetChunk(4, 2), "GetChunk(more chunks than items, index 2)");
        SequenceEqual(Array.Empty<int>(), new[] { 0, 1 }.GetChunk(4, 3), "GetChunk(more chunks than items, index 3)");

        Throws<ArgumentNullException>(() => IEnumerableExstension.GetChunk<int>(null!, 1, 0),
            "GetChunk(null source) did not reject null.");
        Throws<ArgumentOutOfRangeException>(() => values.GetChunk(0, 0),
            "GetChunk(chunkCount 0) did not reject the invalid count.");
        Throws<ArgumentOutOfRangeException>(() => values.GetChunk(-1, 0),
            "GetChunk(chunkCount -1) did not reject the invalid count.");
        Throws<ArgumentOutOfRangeException>(() => values.GetChunk(3, -1),
            "GetChunk(chunkIndex -1) did not reject the invalid index.");
        Throws<ArgumentOutOfRangeException>(() => values.GetChunk(3, 3),
            "GetChunk(chunkIndex equal to chunkCount) did not reject the invalid index.");
    }

    private static void VerifyShuffle()
    {
        var values = new List<int> { 3, 1, 3, 2, 1, 3 };
        var expectedCounts = values.GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
        values.Shuffle();
        var actualCounts = values.GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
        Assert(expectedCounts.Count == actualCounts.Count &&
               expectedCounts.All(pair => actualCounts.TryGetValue(pair.Key, out var count) && count == pair.Value),
            $"Shuffle changed the multiset. Expected [{FormatCounts(expectedCounts)}], actual [{FormatCounts(actualCounts)}].");

        var empty = new List<int>();
        empty.Shuffle();
        SequenceEqual(Array.Empty<int>(), empty, "Shuffle(empty list)");

        var singleton = new List<string> { "only" };
        singleton.Shuffle();
        SequenceEqual(["only"], singleton, "Shuffle(singleton list)");
    }

    private static void VerifyPracticeCategories()
    {
        foreach (var (category, expected) in new[]
                 {
                     (WordPracticeCategory.Known, "known"),
                     (WordPracticeCategory.Rehearsing, "rehearsing"),
                     (WordPracticeCategory.Training, "training")
                 })
        {
            var queryValue = category.ToQueryValue();
            Equal(expected, queryValue, $"ToQueryValue({category})");
            Equal(category, WordPracticeCategoryExtensions.ParseQueryValue(queryValue),
                $"ParseQueryValue(ToQueryValue({category}))");
            Equal(category, WordPracticeCategoryExtensions.ParseQueryValue($"  {queryValue.ToUpperInvariant()}  "),
                $"ParseQueryValue(trimmed upper-case {category})");
        }

        Throws<ArgumentException>(() => WordPracticeCategoryExtensions.ParseQueryValue(null),
            "ParseQueryValue(null) did not reject the missing category.");
        Throws<ArgumentException>(() => WordPracticeCategoryExtensions.ParseQueryValue(""),
            "ParseQueryValue(empty) did not reject the missing category.");
        Throws<ArgumentException>(() => WordPracticeCategoryExtensions.ParseQueryValue("   "),
            "ParseQueryValue(blank) did not reject the missing category.");
        Throws<ArgumentException>(() => WordPracticeCategoryExtensions.ParseQueryValue("unknown"),
            "ParseQueryValue(unknown) did not reject the unknown category.");
    }

    private static void VerifyQuestionConversions()
    {
        var word = new Word { Japanese = "猫", Kana = "ねこ", English = "cat" };
        AssertQuestion(new[] { word }.EnglishToRomajiQuestions().Single(), "cat", "neko",
            "EnglishToRomajiQuestions(cat)");
        AssertQuestion(new[] { word }.JapaneseToEnglishQuestions().Single(), "猫", "cat",
            "JapaneseToEnglishQuestions(猫)");
        AssertQuestion(new[] { word }.KanaToEnglishQuestions().Single(), "ねこ", "cat",
            "KanaToEnglishQuestions(ねこ)");

        Conjugatabel conjugatabel = new Verb
        {
            Japanese = "する",
            Kana = "する",
            English = "do",
            Type = "irregular"
        };
        Equal("ir", conjugatabel.TypeShortForm(), "TypeShortForm(irregular)");
        AssertQuestion(new[] { conjugatabel }.KanaToTypeQuestion().Single(), "する", "ir",
            "KanaToTypeQuestion(irregular)");
        AssertQuestion(new[] { conjugatabel }.EnglishToRomajiAndTypeQuestion().Single(), "do", "suru;ir",
            "EnglishToRomajiAndTypeQuestion(irregular)");
    }

    private static void VerifyRomajiNormalization()
    {
        Equal("koohii", "コーヒー".ToRomaji(), "ToRomaji(コーヒー)");
        Equal("na", "んあ".ToRomaji(), "ToRomaji removes WanaKana apostrophes");
        Equal("kana", "かな(注)".ToRomaji(), "ToRomaji removes parenthesized annotations");
        Equal("kana", "かな~".ToRomaji(), "ToRomaji removes tildes");
    }

    private static void VerifyVerbConjugations()
    {
        var cases = new[]
        {
            VerbCase("買う", "かう", "u", ["買います", "買いません", "買いました", "買いませんでした"], ["かいます", "かいません", "かいました", "かいませんでした"]),
            VerbCase("書く", "かく", "u", ["書きます", "書きません", "書きました", "書きませんでした"], ["かきます", "かきません", "かきました", "かきませんでした"]),
            VerbCase("泳ぐ", "およぐ", "u", ["泳ぎます", "泳ぎません", "泳ぎました", "泳ぎませんでした"], ["およぎます", "およぎません", "およぎました", "およぎませんでした"]),
            VerbCase("話す", "はなす", "u", ["話します", "話しません", "話しました", "話しませんでした"], ["はなします", "はなしません", "はなしました", "はなしませんでした"]),
            VerbCase("待つ", "まつ", "u", ["待ちます", "待ちません", "待ちました", "待ちませんでした"], ["まちます", "まちません", "まちました", "まちませんでした"]),
            VerbCase("死ぬ", "しぬ", "u", ["死にます", "死にません", "死にました", "死にませんでした"], ["しにます", "しにません", "しにました", "しにませんでした"]),
            VerbCase("遊ぶ", "あそぶ", "u", ["遊びます", "遊びません", "遊びました", "遊びませんでした"], ["あそびます", "あそびません", "あそびました", "あそびませんでした"]),
            VerbCase("読む", "よむ", "u", ["読みます", "読みません", "読みました", "読みませんでした"], ["よみます", "よみません", "よみました", "よみませんでした"]),
            VerbCase("帰る", "かえる", "u", ["帰ります", "帰りません", "帰りました", "帰りませんでした"], ["かえります", "かえりません", "かえりました", "かえりませんでした"]),
            VerbCase("食べる", "たべる", "ru", ["食べます", "食べません", "食べました", "食べませんでした"], ["たべます", "たべません", "たべました", "たべませんでした"]),
            VerbCase("する", "する", "irregular", ["します", "しません", "しました", "しませんでした"], ["します", "しません", "しました", "しませんでした"]),
            VerbCase("くる", "くる", "irregular", ["きます", "きません", "きました", "きませんでした"], ["きます", "きません", "きました", "きませんでした"])
        };

        var conjugations = Enum.GetValues<Conjugatabel.ConjugationType>();
        foreach (var testCase in cases)
        {
            for (var index = 0; index < conjugations.Length; index++)
            {
                var conjugation = conjugations[index];
                Equal(testCase.JapaneseExpected[index],
                    testCase.Verb.Conjugate(Conjugatabel.ToConjugate.Japanese, conjugation),
                    $"Verb {testCase.Verb.Japanese} Japanese {conjugation}");
                Equal(testCase.KanaExpected[index],
                    testCase.Verb.Conjugate(Conjugatabel.ToConjugate.Kana, conjugation),
                    $"Verb {testCase.Verb.Kana} kana {conjugation}");
            }
        }
    }

    private static void VerifyVerbTeForms()
    {
        var cases = new[]
        {
            TeCase("買う", "かう", "u", "買って", "かって"),
            TeCase("書く", "かく", "u", "書いて", "かいて"),
            TeCase("泳ぐ", "およぐ", "u", "泳いで", "およいで"),
            TeCase("話す", "はなす", "u", "話して", "はなして"),
            TeCase("待つ", "まつ", "u", "待って", "まって"),
            TeCase("死ぬ", "しぬ", "u", "死んで", "しんで"),
            TeCase("遊ぶ", "あそぶ", "u", "遊んで", "あそんで"),
            TeCase("読む", "よむ", "u", "読んで", "よんで"),
            TeCase("帰る", "かえる", "u", "帰って", "かえって"),
            TeCase("食べる", "たべる", "ru", "食べて", "たべて"),
            TeCase("する", "する", "irregular", "して", "して"),
            TeCase("くる", "くる", "irregular", "きて", "きて")
        };

        foreach (var testCase in cases)
        {
            Equal(testCase.JapaneseExpected,
                testCase.Verb.Form(Conjugatabel.ToConjugate.Japanese, Verb.VerbForm.TE),
                $"Verb {testCase.Verb.Japanese} Japanese TE form");
            Equal(testCase.KanaExpected,
                testCase.Verb.Form(Conjugatabel.ToConjugate.Kana, Verb.VerbForm.TE),
                $"Verb {testCase.Verb.Kana} kana TE form");
        }
    }

    private static void VerifyAdjectiveConjugations()
    {
        var high = new Adjective { Japanese = "高い", Kana = "たかい", English = "high", Type = "i" };
        AssertConjugation(high, Conjugatabel.ConjugationType.PresentNegative, "高くないです", "たかくないです");
        AssertConjugation(high, Conjugatabel.ConjugationType.PastAffirmative, "高かったです", "たかかったです");
        AssertConjugation(high, Conjugatabel.ConjugationType.PastNegative, "高くなかったです", "たかくなかったです");

        var quiet = new Adjective { Japanese = "静か", Kana = "しずか", English = "quiet", Type = "na" };
        AssertConjugation(quiet, Conjugatabel.ConjugationType.PresentAffirmative, "静かです", "しずかです");
        AssertConjugation(quiet, Conjugatabel.ConjugationType.PresentNegative, "静かじゃないです", "しずかじゃないです");
        AssertConjugation(quiet, Conjugatabel.ConjugationType.PastAffirmative, "静かでした", "しずかでした");
        AssertConjugation(quiet, Conjugatabel.ConjugationType.PastNegative, "静かじゃなかったです", "しずかじゃなかったです");

        var good = new Adjective { Japanese = "いい", Kana = "いい", English = "good", Type = "irregular" };
        AssertConjugation(good, Conjugatabel.ConjugationType.PresentAffirmative, "いいです", "いいです");
        AssertConjugation(good, Conjugatabel.ConjugationType.PresentNegative, "よくないです", "よくないです");
        AssertConjugation(good, Conjugatabel.ConjugationType.PastAffirmative, "よかったです", "よかったです");
        AssertConjugation(good, Conjugatabel.ConjugationType.PastNegative, "よくなかったです", "よくなかったです");
    }

    private static void VerifyCounting()
    {
        var generator = new NumbersGenerator();
        var cases = new (int Number, string Expected)[]
        {
            (0, "zero"),
            (1, "ichi"),
            (10, "juu"),
            (11, "juuichi"),
            (20, "nijuu"),
            (99, "kyuujuukyuu"),
            (100, "hyaku"),
            (300, "sanbyaku"),
            (600, "roppyaku"),
            (800, "happyaku"),
            (1_000, "sen"),
            (3_000, "sanzen"),
            (8_000, "hassen"),
            (10_000, "ichiman"),
            (10_001, "ichimanichi"),
            (99_999, "kyuumankyuusenkyuuhyakukyuujuukyuu")
        };

        foreach (var (number, expected) in cases)
        {
            AssertQuestion(generator.GenerateCounting(number), number.ToString(), expected,
                $"GenerateCounting({number})");
        }
    }

    private static void VerifyTimes()
    {
        var generator = new NumbersGenerator();
        var readings = new[]
        {
            "", "ichi", "ni", "san", "yo", "go", "roku", "shichi", "hachi", "ku", "juu", "juuichi", "juuni"
        };

        for (var hour = 1; hour <= 12; hour++)
        {
            AssertTime(generator.GenerateTime(hour), hour, readings[hour], $"GenerateTime({hour})");
        }

        Throws<ArgumentOutOfRangeException>(() => generator.GenerateTime(0),
            "GenerateTime(0) did not reject the invalid hour.");
        Throws<ArgumentOutOfRangeException>(() => generator.GenerateTime(13),
            "GenerateTime(13) did not reject the invalid hour.");
        Throws<ArgumentOutOfRangeException>(() => generator.GenerateRandomTime(0, 12),
            "GenerateRandomTime(0, 12) did not reject the invalid lower bound.");
        Throws<ArgumentOutOfRangeException>(() => generator.GenerateRandomTime(1, 13),
            "GenerateRandomTime(1, 13) did not reject the invalid upper bound.");
        Throws<ArgumentException>(() => generator.GenerateRandomTime(8, 7),
            "GenerateRandomTime(8, 7) did not reject the reversed range.");
        Throws<ArgumentOutOfRangeException>(() => generator.GenerateTimeRange(0, 12).ToArray(),
            "GenerateTimeRange(0, 12) did not reject the invalid lower bound.");
        Throws<ArgumentOutOfRangeException>(() => generator.GenerateTimeRange(1, 13).ToArray(),
            "GenerateTimeRange(1, 13) did not reject the invalid upper bound.");
        Throws<ArgumentException>(() => generator.GenerateTimeRange(8, 7).ToArray(),
            "GenerateTimeRange(8, 7) did not reject the reversed range.");

        AssertTime(generator.GenerateRandomTime(5, 5), 5, readings[5], "GenerateRandomTime(5, 5)");
        var singletonRange = generator.GenerateTimeRange(5, 5).ToArray();
        Equal(1, singletonRange.Length, "GenerateTimeRange(5, 5) result count");
        AssertTime(singletonRange[0], 5, readings[5], "GenerateTimeRange(5, 5) result");
    }

    private static void VerifyKanjiNumbers()
    {
        var generator = new NumbersGenerator();
        var cases = new (int Number, string Expected)[]
        {
            (1, "一"),
            (10, "十"),
            (100, "百"),
            (1_000, "千"),
            (10_000, "一万"),
            (12_345, "一万二千三百四十五")
        };

        foreach (var (number, expected) in cases)
        {
            AssertQuestion(generator.GenerateKanji(number), expected, number.ToString(),
                $"GenerateKanji({number})");
        }
    }

    private static void AssertConjugation(
        Adjective adjective,
        Conjugatabel.ConjugationType conjugation,
        string japaneseExpected,
        string kanaExpected)
    {
        Equal(japaneseExpected, adjective.Conjugate(Conjugatabel.ToConjugate.Japanese, conjugation),
            $"Adjective {adjective.Japanese} Japanese {conjugation}");
        Equal(kanaExpected, adjective.Conjugate(Conjugatabel.ToConjugate.Kana, conjugation),
            $"Adjective {adjective.Kana} kana {conjugation}");
    }

    private static void AssertTime(QuestionAnswerDto result, int hour, string reading, string name)
    {
        var questionPrefix = result.Question.StartsWith($"{hour}:30", StringComparison.Ordinal)
            ? $"{hour}:30"
            : hour.ToString();
        var halfPast = questionPrefix.EndsWith(":30", StringComparison.Ordinal);
        var period = result.Question.EndsWith(" am", StringComparison.Ordinal)
            ? (Question: " am", Answer: "gozen")
            : result.Question.EndsWith(" pm", StringComparison.Ordinal)
                ? (Question: " pm", Answer: "gogo")
                : throw new InvalidOperationException(
                    $"{name} returned a question without an AM/PM suffix. Actual '{result.Question}'.");

        Equal(questionPrefix + period.Question, result.Question, $"{name} question shape");
        Equal(period.Answer + reading + "ji" + (halfPast ? "han" : ""), result.Answer, $"{name} answer");
    }

    private static void AssertQuestion(QuestionAnswerDto actual, string expectedQuestion, string expectedAnswer, string name)
    {
        Equal(expectedQuestion, actual.Question, $"{name} question");
        Equal(expectedAnswer, actual.Answer, $"{name} answer");
    }

    private static (Verb Verb, string[] JapaneseExpected, string[] KanaExpected) VerbCase(
        string japanese,
        string kana,
        string type,
        string[] japaneseExpected,
        string[] kanaExpected) =>
        (new Verb { Japanese = japanese, Kana = kana, English = "test", Type = type }, japaneseExpected, kanaExpected);

    private static (Verb Verb, string JapaneseExpected, string KanaExpected) TeCase(
        string japanese,
        string kana,
        string type,
        string japaneseExpected,
        string kanaExpected) =>
        (new Verb { Japanese = japanese, Kana = kana, English = "test", Type = type }, japaneseExpected, kanaExpected);

    private static string FormatCounts<T>(IReadOnlyDictionary<T, int> counts) where T : notnull =>
        string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string name)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        Assert(expectedArray.SequenceEqual(actualArray),
            $"{name} expected [{string.Join(", ", expectedArray)}], actual [{string.Join(", ", actualArray)}].");
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        Assert(EqualityComparer<T>.Default.Equals(expected, actual),
            $"{name} expected '{expected}', actual '{actual}'.");
    }

    private static void Throws<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{message} Expected {typeof(TException).Name}, actual {exception.GetType().Name}.", exception);
        }

        throw new InvalidOperationException(
            $"{message} Expected {typeof(TException).Name}, actual no exception.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
