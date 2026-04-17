using DataLoaders.Models;
using Repositories.DTOs;

namespace Repositories.Exstensions;

public static class ConjugatabelExstension
{
    public static IEnumerable<QuestionAnswerDto> KanaToTypeQuestion(this IEnumerable<Conjugatabel> data) =>
        data.Select(c => new QuestionAnswerDto
        {
            Question = c.Kana,
            Answer = c.TypeShortForm()
        });

    public static IEnumerable<QuestionAnswerDto> EnglishToRomajiAndTypeQuestion(this IEnumerable<Conjugatabel> data)
        => data.Select(c => new QuestionAnswerDto
        {
            Question = c.English,
            Answer = $"{c.Kana.ToRomaji()};{c.TypeShortForm()}"
        });
}