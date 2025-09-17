using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public abstract class QuestionAnswerBase {
    private readonly List<QuestionAnswerDto> _questionAnswers;
        
    public List<QuestionAnswerDto> QuestionAnswers => _questionAnswers.ToList();

    protected QuestionAnswerBase(string folderPath, string fileName)
    {
        var loader = new QuestionAnswerLoader(folderPath, fileName);
        _questionAnswers = loader.Elements.Select(QuestionAnswerDto.FromModel).ToList();
    }
}