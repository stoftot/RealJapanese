using DataLoaders.Models;
using Repositories.DTOs;
using WanaKanaSharp;

namespace DataLoaders.Exstensions;

public static class WordExstensions
{
    public static IEnumerable<QuestionAnswerDto> EnglishToRomajiQuestions(this IEnumerable<Word> words) =>
        words.Select(v => new QuestionAnswerDto
        {
            Answer = WanaKana.ToRomaji(v.Kana),
            Question = v.English
        });
}