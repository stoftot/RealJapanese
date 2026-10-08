using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Verbs;

public class ConjugationsAndFormsBase : SingleAnwserBase
{
    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        var questions = new List<QuestionAnswerDto>();
        
        //conjugation
        // foreach (var ending in Verb.possibleEndings)
        // {
        //     foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
        //     {
        //         questions.Add(new()
        //         {
        //             Question = ending.Kana + " - " + conjugationType.ToDisplayString(),
        //             Answer = ending.Conjugate(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
        //         });
        //     }
        // }
        
        //te form
        // foreach (var ending in Verb.allPossibleEndings)
        // {
        //     foreach (var verbForm in Enum.GetValues<Verb.VerbForm>())
        //     {
        //         if(verbForm == Verb.VerbForm.TA)continue;
        //         questions.Add(new ()
        //         {
        //             Question = (ending.Type == "ru"?"(ru)":"") + ending.Kana + " - " + verbForm + " form",
        //             Answer = ending.Form(Conjugatabel.ToConjugate.Kana, verbForm).ToRomaji()
        //         });
        //     }
        // }
        //
        //action/change form
        // foreach (var ending in Verb.possibleEndings)
        // {
        //     foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
        //     {
        //         questions.Add(new()
        //         {
        //             Question = ending.Kana + " - " + conjugationType.ToDisplayString(),
        //             Answer = ending.ActionOrChangeForm(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
        //         });
        //     }
        // }
        //
        //short form
        foreach (var ending in Verb.possibleEndings)
        {
            foreach (var conjugationType in Enum.GetValues<Conjugatabel.ConjugationType>())
            {
                questions.Add(new()
                {
                    Question = ending.Kana + " - " + conjugationType.ToDisplayString() + " - " + "short",
                    Answer = ending.ShortForm(Conjugatabel.ToConjugate.Kana, conjugationType).ToRomaji()
                });
            }
        }
        
        OrginalQuestions = questions;
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}