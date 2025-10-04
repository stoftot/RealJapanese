using System.Text;
using Repositories.DTOs;

namespace Repositories;

public class NumbersGenerator
{
    private FlexibleDictionary Counting { get; } = new (new Dictionary<string, string>()
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

    private FlexibleDictionary Time { get; } = new(new Dictionary<string, string>
    {
        ["4"] = "yo",
        ["7"] = "shichi",
        ["9"] = "kyuu"
    });

    private FlexibleDictionary Age { get; } = new(new Dictionary<string, string>
    {
        ["9"] = "kyuu"
    });

    #region Counting generators

    public QuestionAnswerDto GenerateOneCounting(int number)
    {
        return new QuestionAnswerDto
        {
            Answer = GenerateNumber(number.ToString(), Counting),
            Question = number.ToString()
        };
    }
    public QuestionAnswerDto GenerateOneFromRangeCounting(int lowerRange, int upperRange)
    {
        var number = new Random().Next(lowerRange, upperRange + 1);

        return GenerateOneCounting(number);
    }
    public List<QuestionAnswerDto> GenerateRangeCounting(int lowerRange, int upperRange)
    {
        var output = new List<QuestionAnswerDto>();

        for (int i = lowerRange; i <= upperRange; i++)
        {
            output.Add(new QuestionAnswerDto()
            {
                Answer = GenerateNumber(i.ToString(), Counting),
                Question = i.ToString()
            });
        }

        return output;
    }

    #endregion

    #region Time generators

    public QuestionAnswerDto GenerateOneTime(int number)
    {
        ValidateTime(number);
        
        return GenerateTime(number.ToString());
    }
    public QuestionAnswerDto GenerateOneFromRangeTime(int lowerRange, int upperRange)
    {
        ValidateTimeRange(lowerRange, upperRange);
        
        var number = new Random().Next(lowerRange, upperRange + 1);

        return GenerateTime(number.ToString());
    }
    public List<QuestionAnswerDto> GenerateRangeTime(int lowerRange, int upperRange)
    {
        ValidateTimeRange(lowerRange, upperRange);
        
        var output = new List<QuestionAnswerDto>();

        for (int i = lowerRange; i <= upperRange; i++)
        {
            output.Add(GenerateTime(i.ToString()));
        }

        return output;
    }
    private QuestionAnswerDto GenerateTime(string number)
    {
        var numbers = new FlexibleDictionary(Counting);
        foreach (var (key, value) in Time) numbers[key] = value;

        var generatedNumber = GenerateNumber(number, numbers);

        var generatedTime = new StringBuilder();
        var question = new StringBuilder(generatedNumber);
        //am or pm
        if (new Random().Next(2) == 1)
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
        if (new Random().Next(2) == 1)
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
        if (number is < 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(number), $"{number} is not a valid time");
        }
    }
    private static void ValidateTimeRange(int lowerRange, int upperRange)
    {
        if (lowerRange < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lowerRange), $"lower range must be >= 0");
        }

        if (upperRange > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(upperRange), $"upper range must be <= 12");
        }
    }
    
    #endregion

    #region Age generators
    
    public QuestionAnswerDto GenerateOneAge(int number)
    {
        return GenerateAge(number.ToString());
    }
    public QuestionAnswerDto GenerateOneFromRangeAge(int lowerRange, int upperRange)
    {
        var number = new Random().Next(lowerRange, upperRange + 1).ToString();
        return GenerateAge(number);
    }
    public List<QuestionAnswerDto> GenerateRangeAge(int lowerRange, int upperRange)
    {
        var output = new List<QuestionAnswerDto>();

        for (int i = lowerRange; i <= upperRange; i++)
        {
            output.Add(GenerateAge(i.ToString()));
        }

        return output;
    }
    private QuestionAnswerDto GenerateAge(string number)
    {
        var numbers = new FlexibleDictionary(Counting);
        foreach (var (key, value) in Age) numbers[key] = value;

        return new QuestionAnswerDto
        {
            Answer = GenerateNumber(number, numbers) + "sai",
            Question = number + " years old"
        };
    }

    #endregion
    

    #region Generate number helpers

    private string GenerateNumber(string number, FlexibleDictionary numbers)
    {
        if (Counting.TryGetValue(number, out var value))
        {
            return value;
        }

        return number.Length switch
        {
            2 => GenerateTens(number, numbers),
            3 => GenerateHundreds(number, numbers),
            4 => GenerateThousands(number, numbers),
            5 => GenerateTenThousands(number, numbers),
            _ => throw new ArgumentOutOfRangeException($"{number}, is not a supported number")
        };
    }
    private static string GenerateTens(string twoDigits, FlexibleDictionary numbers)
    {
        var firstDigit = twoDigits[0];
        var lastDigit = twoDigits[1];

        switch (firstDigit)
        {
            case '0' when lastDigit == '0':
                return "";
            case '0':
                return numbers[lastDigit];
        }

        if (firstDigit == '1') return numbers["10"] + numbers[lastDigit];

        var tens = numbers[firstDigit] + numbers["10"];
        if (lastDigit == '0') return tens;

        return tens + numbers[lastDigit];
    }

    private static string GenerateHundreds(string number, FlexibleDictionary numbers)
    {
        return GenerateTemplate(number, numbers, "100", GenerateTens);
    }

    private static string GenerateThousands(string number, FlexibleDictionary numbers)
    {
        return GenerateTemplate(number, numbers, "1000", GenerateHundreds);
    }

    private static string GenerateTenThousands(string number, FlexibleDictionary numbers)
    {
        var firstDigit = number[0];
        var lastDigits = number[1..];

        var tenThousands = numbers[firstDigit] + numbers[10_000];

        var tail = GenerateThousands(lastDigits, numbers);
        return string.IsNullOrEmpty(tail) ? tenThousands : tenThousands + tail;
    }

    private static string GenerateTemplate(string number,
        FlexibleDictionary numbers,
        string baseNumber, Func<string, FlexibleDictionary, string> restOfNumbers)
    {
        var firstDigit = number[0];
        var lastDigits = number[1..];

        var firstNumber =
            firstDigit == '1'
                ? numbers[baseNumber]
                : numbers[firstDigit] + numbers[baseNumber];

        var tail = restOfNumbers(lastDigits, numbers);
        return string.IsNullOrEmpty(tail) ? firstNumber : firstNumber + tail;
    }

    #endregion
    
    private sealed class FlexibleDictionary(Dictionary<string, string> dict) : Dictionary<string, string>(dict)
    {
        public string this[int key] => this[key.ToString()];

        public string this[char key] => this[key.ToString()];
    }
}