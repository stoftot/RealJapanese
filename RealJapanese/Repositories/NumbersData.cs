using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class NumbersData
{
    private string FolderPath => "../Data";
    private string FileName => "Numbers.csv";
    
    public List<QuestionAnswerDto> QuestionAnswers = [];
    
    public NumbersData()
    {
        var loader = new QuestionAnswerLoader(FolderPath, FileName);
        QuestionAnswers = loader.Elements.Select(QuestionAnswerDto.FromModel).ToList();
    }
}