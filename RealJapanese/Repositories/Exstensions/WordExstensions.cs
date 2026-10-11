using DataLoaders.Models;
using Repositories.DTOs;
using Repositories.Bases;

namespace Repositories.Exstensions;

public static class WordExstensions
{
    public static IEnumerable<QuestionAnswerDto> EnglishToRomajiQuestions(this IEnumerable<Word> words) =>
        words.Select(v => new QuestionAnswerDto
        {
            Answer = v.Kana.ToRomaji(),
            Question = v.English,
            SourceWord = v
        });
    
    public static IEnumerable<QuestionAnswerDto> JapaneseToEnglishQuestions(this IEnumerable<Word> words) =>
        words.Select(v => new QuestionAnswerDto
        {
            Answer = v.English,
            Question = v.Japanese,
            SourceWord = v
        });
    
    public static IEnumerable<QuestionAnswerDto> KanaToEnglishQuestions(this IEnumerable<Word> words) =>
        words.Select(v => new QuestionAnswerDto
        {
            Answer = v.English,
            Question = v.Kana
        });

    public static IEnumerable<QuestionAnswerDto> TrackProgress<T>(this IEnumerable<QuestionAnswerDto> questions,
        WordDataBase<T> repository) where T : Word => questions.TrackProgress((word, correct) =>
        repository.RecordPracticeAnswer(word.Id, correct));

    public static IEnumerable<QuestionAnswerDto> TrackProgress(this IEnumerable<QuestionAnswerDto> questions,
        Action<Word, bool> recordAnswer) => questions.Select(question =>
    {
        if (question.SourceWord is { } sourceWord)
            question.RecordAnswer = correct => recordAnswer(sourceWord, correct);
        return question;
    });
}
