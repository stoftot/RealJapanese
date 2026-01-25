using DataLoaders.Models;
using Repositories.DTOs;

namespace Repositories.Exstensions;

public static class WordExstensions
{
    public static IEnumerable<QuestionAnswerDto> EnglishToRomajiQuestions(this IEnumerable<Word> words) =>
        words.Select(v => new QuestionAnswerDto
        {
            Answer = v.Kana.ToRomaji(),
            Question = v.English
        });
}