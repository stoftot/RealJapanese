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
        
        //conjugate
        foreach (var ending in Adjective.possibleEndings)
        {
            foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
            {
                questions.Add(new QuestionAnswerDto
                {
                    Question = ending.Kana + " - " + conjugationType.ToDisplayString(),
                    Answer = ending.Conjugate(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
                });
            }
        }
        //te form
        foreach (var ending in Adjective.possibleEndings)
        {

            questions.Add(new QuestionAnswerDto
            {
                Question = ending.Kana + " - Te form",
                Answer = ending.TeForm(Conjugatabel.ToConjugate.Kana).ToRomaji()
            });
        }
        //short form
        foreach (var ending in Adjective.possibleEndings)
        {
            foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
            {
                questions.Add(new QuestionAnswerDto
                {
                    Question = ending.Kana + " - " + conjugationType.ToDisplayString() + " - short",
                    Answer = ending.Conjugate(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
                });
            }
        }
        
        
        
        OrginalQuestions = questions;
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}