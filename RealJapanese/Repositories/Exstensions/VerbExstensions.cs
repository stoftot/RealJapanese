using DataLoaders.Models;
using Repositories.DTOs;

namespace Repositories.Exstensions;

public static class VerbExstensions
{
    public static IEnumerable<QuestionAnswerDto> EnglishToRomajiAndTypeQuestion(this IEnumerable<Verb> verbs)
        => verbs.Select(v => new QuestionAnswerDto
        {
            Question = v.English,
            Answer = $"{v.Kana.ToRomaji()};{v.TypeShortForm()}"
        });
}