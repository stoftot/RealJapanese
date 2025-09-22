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
    
    public string? GenerateCounting(int loverRange, int upperRange) // kept your param name
    {
        var number = new Random().Next(loverRange, upperRange).ToString();

        if (Counting.TryGetValue(number, out var value))
        {
            return value;
        }

        return number.Length switch
        {
            2 => GenerateTens(number),
            3 => GenerateHundreds(number),
            4 => GenerateThousands(number),
            _ => null
        };
    }

    #region Counting helpers

    private string GenerateTens(string twoDigits)
    {
        var t = twoDigits[0];
        var u = twoDigits[1];

        switch (t)
        {
            case '0' when u == '0':
                return "";
            case '0':
                return Counting[u.ToString()];
        }

        var tens = Counting[t.ToString()] + Counting["10"];
        if (u == '0') return tens;

        return tens + Counting[u.ToString()];
    }

    private string GenerateHundreds(string threeDigits)
    {
        var h = threeDigits[0];
        var lastTwo = threeDigits[1..];

        if (h == '0') return GenerateTens(lastTwo);

        var hundreds = (h == '1')
            ? Counting["100"]
            : Counting[h.ToString()] + Counting["100"];

        var tail = GenerateTens(lastTwo);
        return hundreds + tail;
    }

    private string GenerateThousands(string fourDigits)
    {
        var th = fourDigits[0];
        var lastThree = fourDigits[1..];

        var thousands = (th == '1')
            ? Counting["1000"]
            : Counting[th.ToString()] + Counting["1000"];

        var tail = GenerateHundreds(lastThree);
        return string.IsNullOrEmpty(tail) ? thousands : thousands + tail;
    }

    #endregion
    
    
}