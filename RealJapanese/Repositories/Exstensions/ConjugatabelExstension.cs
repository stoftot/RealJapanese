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
}