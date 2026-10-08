using DataLoaders.Models;
using RealJapanese.Components.Shared;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Nouns;

public class NounConjugateBase : SingleAnwserBase
{
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        // An empty noun stem produces only the ending being practised.
        var noun = new Noun { Japanese = "", Kana = "", English = "noun" };
        var questions = new List<QuestionAnswerDto>();

        foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
        {
            questions.Add(new()
            {
                Question = "Noun - " + conjugationType.ToDisplayString() + " - polite",
                Answer = noun.Conjugate(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
            });
            questions.Add(new()
            {
                Question = "Noun - " + conjugationType.ToDisplayString() + " - short",
                Answer = noun.ShortForm(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
            });
        }

        questions.Add(new()
        {
            Question = "Noun - te form",
            Answer = noun.TeForm(Conjugatabel.ToConjugate.Kana).ToRomaji()
        });

        OrginalQuestions = questions;
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef?.ClearAndFocusInputAsync() ?? Task.CompletedTask;
}
