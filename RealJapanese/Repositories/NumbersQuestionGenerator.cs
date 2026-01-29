using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class NumbersQuestionGenerator
{
    private NumbersGenerator NumbersGenerator { get; } = new();
    
    // private readonly FlexibleDictionary kanji = new(new Dictionary<string, string>
    // {
    //     { "1", "一" },
    //     { "2", "二" },
    //     { "3", "三" },
    //     { "4", "四" },
    //     { "5", "五" },
    //     { "6", "六" },
    //     { "7", "七" },
    //     { "8", "八" },
    //     { "9", "九" },
    //     { "10", "十" },
    //     { "100", "百" },
    //     { "1000", "千" },
    //     { "10000", "万" }
    // });
    
    public QuestionAnswerDto GenerateRandomQuestion()
    {
        return Random.Shared.Next(0, 3) switch

        {
            0 => GenerateRandomCountingQuestion(),
            1 => GenerateRandomAgeQuestion(),
            2 => GenerateRandomTimeQuestion(),
        };
    }
    
    public QuestionAnswerDto GenerateRandomCountingQuestion(int lowerRange = 0, int upperRange = 99_999)
    {
        return NumbersGenerator.GenerateRandomCounting(lowerRange, upperRange);
    }
    
    public QuestionAnswerDto GenerateRandomAgeQuestion(int lowerRange = 0, int upperRange = 120)
    {
        return NumbersGenerator.GenerateRandomAge(lowerRange, upperRange);
    }
    
    public QuestionAnswerDto GenerateRandomTimeQuestion(int lowerRange = 0, int upperRange = 12)
    {
        return NumbersGenerator.GenerateRandomTime(lowerRange, upperRange);
    }

    public IEnumerable<QuestionAnswerDto> GenerateAllSpecialCases()
    {
        return NumbersGenerator.GenerateAllSpecialCases();
    }
    
    public IEnumerable<QuestionAnswerDto> GenerateAllUniq()
    {
        return GenerateALlUniqCounting()
            .Concat(GenerateALlUniqAge())
            .Concat(GenerateALlUniqTime());
    }

    public IEnumerable<QuestionAnswerDto> GenerateUniqKanji()
    {
        foreach (int number in new[] { 1, 10, 100, 1_000, 10_000 })
        {
            yield return NumbersGenerator.GenerateKanji(number);
        }
    }

    public IEnumerable<QuestionAnswerDto> GenerateALlUniqCounting()
    {
        yield return NumbersGenerator.GenerateCounting(0);
        
        foreach (int multiplier in new[] { 1, 10, 100, 1_000, 10_000 })
        {
            for (int i = 1; i <= 9; i++)
            {
                yield return NumbersGenerator.GenerateCounting(i * multiplier);
            }
        }
    }
    
    public IEnumerable<QuestionAnswerDto> GenerateALlUniqAge()
    {
        foreach (int multiplier in new[] { 1, 10})
        {
            for (int i = 1; i <= 9; i++)
            {
                yield return NumbersGenerator.GenerateAge(i * multiplier);
            }
        }
        
        yield return NumbersGenerator.GenerateAge(100);
    }
    
    public IEnumerable<QuestionAnswerDto> GenerateALlUniqTime()
    {
        return NumbersGenerator.GenerateTimeRange(1, 12);
    }
}