using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class GreetingsData
{
    private static string FolderPath => "../Data";
    private static string FileNameGreetings => "Greetings.csv";
    private static string FileNameExpressions => "Expressions.csv";
    private static string FileNamePerson => "Person.csv";
    private static string FileNameSchool => "School.csv";
    private static string FileNameOther => "Other.csv";
    
    private readonly List<QuestionAnswerDto> _questionAnswers;
        
    public List<QuestionAnswerDto> QuestionAnswers => _questionAnswers.ToList();

    public GreetingsData()
    {
        var greetingsLoader = new QuestionAnswerLoader(FolderPath, FileNameGreetings);
        var expressionsLoader = new QuestionAnswerLoader(FolderPath, FileNameExpressions);
        var personLoader = new QuestionAnswerLoader(FolderPath, FileNamePerson);
        var schoolLoader = new QuestionAnswerLoader(FolderPath, FileNameSchool);
        var otherLoader = new QuestionAnswerLoader(FolderPath, FileNameOther);
        _questionAnswers = [];
        _questionAnswers.AddRange(greetingsLoader.Elements.Select(QuestionAnswerDto.FromModel));
        _questionAnswers.AddRange(expressionsLoader.Elements.Select(QuestionAnswerDto.FromModel));
        _questionAnswers.AddRange(personLoader.Elements.Select(QuestionAnswerDto.FromModel));
        _questionAnswers.AddRange(schoolLoader.Elements.Select(QuestionAnswerDto.FromModel));
        _questionAnswers.AddRange(otherLoader.Elements.Select(QuestionAnswerDto.FromModel));
    }
}