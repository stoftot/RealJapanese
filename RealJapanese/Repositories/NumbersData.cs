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
        _questionAnswers.AddRange(NumbersGenerator.GenerateAllCounting(111,130));
        
    }
}