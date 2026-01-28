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
    
    public static IEnumerable<QuestionAnswerDto> JapaneseToEnglishQuestions(this IEnumerable<Word> words) =>
        words.Select(v => new QuestionAnswerDto
        {
            Answer = v.English,
            Question = v.Japanese
        });
}