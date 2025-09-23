using System.Text;

namespace Repositories;

public class NumbersGenerator
{
    private Dictionary<string, string> Counting { get; init; }
    private Dictionary<string, string> Time { get; init; }
    private Dictionary<string, string> Age { get; init; }

    public NumbersGenerator()
    {
        Counting = new Dictionary<string, string>()
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
            { "1000", "sen" }
        };

        Time = new Dictionary<string, string>(Counting)
        {
            { "4", "yo" },
            { "7", "shichi" },
            { "9", "ku" }
        };

        Age = new Dictionary<string, string>(Counting)
        {
            { "4", "yo" }
        };
    }

    public string GenerateOneCounting(int lowerRange, int upperRange)
    {
        var number = new Random().Next(lowerRange, upperRange+1).ToString();

        return GenerateNumber(number, Counting);
    }

    public List<string> GenerateAllCounting(int lowerRange, int upperRange)
    {
        var output = new List<string>();
        
        for (int i = lowerRange; i <= upperRange; i++)
        {
            output.Add(GenerateNumber(i.ToString(), Counting));    
        }

        return output;
    }

    
    private string GenerateNumber(string number, Dictionary<string, string> numbers)
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
            _ => throw new ArgumentOutOfRangeException($"{number}, is not a valid number")
        };
    }

    #region Generate number helpers

    private string GenerateTens(string twoDigits, Dictionary<string, string> numbers)
    {
        var t = twoDigits[0];
        var u = twoDigits[1];

        switch (t)
        {
            case '0' when u == '0':
                return "";
            case '0':
                return numbers[u.ToString()];
        }

        var tens = numbers[t.ToString()] + numbers["10"];
        if (u == '0') return tens;

        return tens + numbers[u.ToString()];
    }

    private string GenerateHundreds(string threeDigits,  Dictionary<string, string> numbers)
    {
        var h = threeDigits[0];
        var lastTwo = threeDigits[1..];

        if (h == '0') return GenerateTens(lastTwo,  numbers);

        var hundreds = (h == '1')
            ? numbers["100"]
            : numbers[h.ToString()] + numbers["100"];

        var tail = GenerateTens(lastTwo, numbers);
        return hundreds + tail;
    }

    private string GenerateThousands(string fourDigits, Dictionary<string, string> numbers)
    {
        var th = fourDigits[0];
        var lastThree = fourDigits[1..];

        var thousands = (th == '1')
            ? numbers["1000"]
            : numbers[th.ToString()] + numbers["1000"];

        var tail = GenerateHundreds(lastThree, numbers);
        return string.IsNullOrEmpty(tail) ? thousands : thousands + tail;
    }

    #endregion
    
    
}