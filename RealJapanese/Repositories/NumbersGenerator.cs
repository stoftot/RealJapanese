using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Repositories.DTOs;

namespace Repositories;

public class NumbersGenerator
{
    private readonly FlexibleDictionary kanji = new(new Dictionary<string, string>
    {
        { "1", "一" },
        { "2", "二" },
        { "3", "三" },
        { "4", "四" },
        { "5", "五" },
        { "6", "六" },
        { "7", "七" },
        { "8", "八" },
        { "9", "九" },
        { "10", "十" },
        { "100", "百" },
        { "1000", "千" },
        { "10000", "万" }
    });
    
    private readonly FlexibleDictionary counting = new(new Dictionary<string, string>
    {
        { "0", "zero" },
        { "1", "ichi" },
        { "2", "ni" },
        { "3", "san" },
        { "4", "yon" },
        { "5", "go" },
        { "6", "roku" },
        { "7", "nana" },
        { "8", "hachi" },
        { "9", "kyuu" },
        { "10", "juu" },
        { "100", "hyaku" },
        { "300", "sanbyaku" },
        { "600", "roppyaku" },
        { "800", "happyaku" },
        { "1000", "sen" },
        { "3000", "sanzen" },
        { "8000", "hassen" }
    });

    private readonly FlexibleDictionary time = new(new Dictionary<string, string>
    {
        { "4", "yo" },
        { "7", "shichi" },
        { "9", "ku" }
    });

    private readonly FlexibleDictionary age = new(new Dictionary<string, string>
    {
        { "9", "ku" }
    });

    public IEnumerable<QuestionAnswerDto> GenerateAllSpecialCases()
    {
        foreach (var (key,_) in counting)
        {
            yield return GenerateCounting(int.Parse(key));
        }

        foreach (var (key,_) in age)
        {
            yield return GenerateAge(int.Parse(key));
        }
        
        foreach (var (key,_) in time)
        {
            yield return GenerateTime(int.Parse(key));
        }
    }


    public IEnumerable<QuestionAnswerDto> GenerateUniqKanji()
    {
        foreach (var (key, _) in kanji)
        {
            yield return GenerateKanji(int.Parse(key));
        }
    }
    public QuestionAnswerDto GenerateKanji(int number) =>
        new()
        {
            Answer = number.ToString(),
            Question = GenerateNumber(number.ToString(), kanji)
        };
    
    
    #region Counting generators

    public QuestionAnswerDto GenerateCounting(int number) =>
        new ()
        {
            Answer = GenerateNumber(number.ToString(), counting),
            Question = number.ToString()
        };

    public QuestionAnswerDto GenerateRandomCounting(int lowerRange, int upperRange)
    {
        var number = Random.Shared.Next(lowerRange, upperRange + 1);

        return GenerateCounting(number);
    }

    public IEnumerable<QuestionAnswerDto> GenerateCountingRange(int lowerRange, int upperRange)
    {
        for (int i = lowerRange; i <= upperRange; i++)
        {
            yield return new QuestionAnswerDto
            {
                Answer = GenerateNumber(i.ToString(), counting),
                Question = i.ToString()
            };
        }
    }

    #endregion

    #region Time generators

    public QuestionAnswerDto GenerateTime(int number)
    {
        ValidateTime(number);

        return GenerateTime(number.ToString());
    }

    public QuestionAnswerDto GenerateRandomTime(int lowerRange, int upperRange)
    {
        ValidateTimeRange(lowerRange, upperRange);

        var number = Random.Shared.Next(lowerRange, upperRange + 1);

        return GenerateTime(number.ToString());
    }

    public IEnumerable<QuestionAnswerDto> GenerateTimeRange(int lowerRange, int upperRange)
    {
        ValidateTimeRange(lowerRange, upperRange);

        for (int i = lowerRange; i <= upperRange; i++)
        {
            yield return GenerateTime(i.ToString());
        }
    }

    private QuestionAnswerDto GenerateTime(string number)
    {
        var numbers = counting.WithOverrides(time);

        var generatedNumber = GenerateNumber(number, numbers);

        var generatedTime = new StringBuilder();
        var question = new StringBuilder(number);
        //am or pm
        if (Random.Shared.Next(2) == 1)
        {
            generatedTime.Append("gozen");
            question.Append(" am");
        }
        else
        {
            generatedTime.Append("gogo");
            question.Append(" pm");
        }

        generatedTime.Append(generatedNumber);

        generatedTime.Append("ji");

        //half past
        if (Random.Shared.Next(2) == 1)
        {
            generatedTime.Append("han");
            question.Insert(question.Length - 3, ":30");
        }


        return new QuestionAnswerDto()
        {
            Answer = generatedTime.ToString(),
            Question = question.ToString()
        };
    }

    private static void ValidateTime(int number)
    {
        if (number is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(number), $"{number} is not a valid time");
        }
    }

    private static void ValidateTimeRange(int lowerRange, int upperRange)
    {
        if (lowerRange < 1)
            throw new ArgumentOutOfRangeException(nameof(lowerRange), $"lower range must be >= 1");
        if (upperRange > 12)
            throw new ArgumentOutOfRangeException(nameof(upperRange), $"upper range must be <= 12");
        if (lowerRange > upperRange)
            throw new ArgumentException("lowerRange cannot be greater than upperRange");
    }

    #endregion

    #region Age generators

    public QuestionAnswerDto GenerateAge(int number) => 
        GenerateAge(number.ToString());

    public QuestionAnswerDto GenerateRandomAge(int lowerRange, int upperRange) =>
        GenerateAge(Random.Shared.Next(lowerRange, upperRange + 1).ToString());

    public IEnumerable<QuestionAnswerDto> GenerateAgeRange(int lowerRange, int upperRange)
    {
        for (int i = lowerRange; i <= upperRange; i++)
        {
            yield return GenerateAge(i.ToString());
        }
    }

    private QuestionAnswerDto GenerateAge(string number)
    {
        var numbers = counting.WithOverrides(age);

        return new QuestionAnswerDto
        {
            Answer = GenerateNumber(number, numbers) + "sai",
            Question = number + " years old"
        };
    }

    #endregion


    #region Generate number helpers

    private static string GenerateNumber(string number, FlexibleDictionary numbers)
    {
        if (number.Length > 1) number = number.TrimStart('0');

        if (numbers.TryGetValue(number, out var value))
        {
            return value;
        }

        return number.Length switch
        {
            0 => "",
            2 => GenerateTens(number, numbers),
            3 => GenerateHundreds(number, numbers),
            4 => GenerateThousands(number, numbers),
            5 => GenerateTenThousands(number, numbers),
            _ => throw new ArgumentOutOfRangeException(nameof(number), number,
                $"Number length \"{number.Length}\" not supported.")
        };
    }

    private static string GenerateTens(string number, FlexibleDictionary numbers)
    {
        var firstDigit = number[0];
        var lastDigit = number[1];

        var head =
            firstDigit == '1' ? numbers[10] : numbers[firstDigit] + numbers[10];
        return lastDigit == '0' ? head : head + numbers[lastDigit];
    }

    private static string GenerateHundreds(string number, FlexibleDictionary numbers) => 
        GenerateTemplate(number, numbers, 
            firstDigit => firstDigit == '1' ? numbers[100] : numbers[firstDigit] + numbers[100]);

    private static string GenerateThousands(string number, FlexibleDictionary numbers) => 
        GenerateTemplate(number, numbers, 
            firstDigit => firstDigit == '1' ? numbers[1_000] : numbers[firstDigit] + numbers[1_000]);

    private static string GenerateTenThousands(string number, FlexibleDictionary numbers) => 
        GenerateTemplate(number, numbers, 
            firstDigit => numbers[firstDigit] + "man");

    private static string GenerateTemplate(
        string number,
        FlexibleDictionary numbers,
        Func<char, string> headGeneration)
    {
        var firstDigit = number[0];
        var lastDigits = number[1..];

        var head = headGeneration(firstDigit);

        var tail = GenerateNumber(lastDigits, numbers);
        return string.IsNullOrEmpty(tail) ? head : head + tail;
    }

    #endregion

    private sealed class FlexibleDictionary(IDictionary<string, string> dict) : IReadOnlyDictionary<string, string>
    {
        private readonly IDictionary<string, string> dict = dict;
        public string this[int key] => dict[key.ToString()];
        public string this[char key] => dict[key.ToString()];
        public string this[string key] => dict[key];

        public IEnumerable<string> Keys => dict.Keys;
        public IEnumerable<string> Values => dict.Values;
        public int Count => dict.Count;
        
        public bool ContainsKey(string key) => dict.ContainsKey(key);
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => dict.TryGetValue(key, out value);
        
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => dict.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        public FlexibleDictionary WithOverrides(IEnumerable<KeyValuePair<string,string>> overrides)
        {
            var newDict = new Dictionary<string, string>(dict);
            foreach (var (key, value) in overrides) newDict[key] = value;
            return new FlexibleDictionary(newDict);
        }
    }
}