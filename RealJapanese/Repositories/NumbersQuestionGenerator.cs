using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class NumbersQuestionGenerator
{
    private NumbersGenerator NumbersGenerator { get; } = new();
    
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
    
    private List<QuestionAnswerDto> FlippedCounting(int lowerRange, int upperRange)
    {
        return NumbersGenerator.GenerateCountingRange(lowerRange,upperRange)
            .Select(qa => new QuestionAnswerDto()
            {
                Question = qa.Answer,
                Answer = qa.Question
            })
            .ToList();
    }
}