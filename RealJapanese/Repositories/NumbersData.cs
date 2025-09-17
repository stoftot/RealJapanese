using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class NumbersData
{
    private static string FolderPath => "../Data";
    private static string FileName => "Numbers.csv";

    private readonly List<QuestionAnswerDto> _questionAnswers;
    
    public List<QuestionAnswerDto> QuestionAnswers => _questionAnswers.ToList();

    public NumbersData()
    {
        var loader = new QuestionAnswerLoader(FolderPath, FileName);
        _questionAnswers = loader.Elements.Select(QuestionAnswerDto.FromModel).ToList();
    }
}