using DataLoaders.Models;
using Repositories.DTOs;

namespace Repositories.Exstensions;

public static class VerbExstensions
{
    public static IEnumerable<QuestionAnswerDto> EnglishToRomajiAndTypeQuestion(this IEnumerable<Verb> verbs)
        => verbs
            // .Where(v => v.Kana.ToRomaji()[^2..] == "ru" && v.TypeShortForm()!="ir")
            .Select(v => new QuestionAnswerDto
        {
            Question = v.English,
            Answer = $"{v.Kana.ToRomaji()};{v.TypeShortForm()}"
        });
}