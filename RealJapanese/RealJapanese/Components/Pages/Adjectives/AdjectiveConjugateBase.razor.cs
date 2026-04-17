using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Adjectives;

public partial class AdjectiveConjugateBaseBase : SingleAnwserBase
{
    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        var questions = new List<QuestionAnswerDto>();
        
        foreach (var ending in Adjective.possibleEndings)
        {
            foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
            {
                questions.Add(new QuestionAnswerDto
                {
                    Question = ending.Kana + " - " + conjugationType.ToDisplayString(),
                    Answer = ending.Conjugate(Conjugatabel.ToConjugate.Japanese, conjugationType).ToRomaji()
                });
            }
        }
        
        OrginalQuestions = questions;
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}