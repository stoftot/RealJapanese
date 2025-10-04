using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class NumbersData
{
    private static string FolderPath => "../Data";
    private static string FileName => "Numbers.csv";
    private static string FileNameTime => "NumbersTime.csv";
    
    private static string FileNameage => "NumbersAge.csv";
    
    private readonly List<QuestionAnswerDto> _questionAnswers;
    
    private NumbersGenerator NumbersGenerator { get; } = new();
        
    public List<QuestionAnswerDto> QuestionAnswers => _questionAnswers.ToList();

    public NumbersData()
    {
        var numbersLoader = new QuestionAnswerLoader(FolderPath, FileName);
        var timeLoader = new QuestionAnswerLoader(FolderPath, FileNameTime);
        var ageLoader = new QuestionAnswerLoader(FolderPath, FileNameage);
        _questionAnswers = [];
        // _questionAnswers.AddRange(numbersLoader.Elements.Select(QuestionAnswerDto.FromModel));
        // _questionAnswers.AddRange(timeLoader.Elements.Select(QuestionAnswerDto.FromModel));
        // _questionAnswers.AddRange(ageLoader.Elements.Select(QuestionAnswerDto.FromModel));
        // _questionAnswers.AddRange(NumbersGenerator.GenerateCountingRange(0,100));
        // _questionAnswers.AddRange(FlippedCounting(0,100));
        _questionAnswers.Add(NumbersGenerator.GenerateCounting(18));
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