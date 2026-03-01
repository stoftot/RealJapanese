using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Adjectives;

public class CategoriesAdjectivesBase : SingleAnwserBase
{
    [SupplyParameterFromQuery(Name = "training")]
    public bool Training { get; set; } = false;
    
    [Inject]
    public AdjectiveData AdjectiveData { get; set; } = null!;

    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = (Training ? AdjectiveData.TrainingWords : AdjectiveData.VocabWords)
            .KanaToTypeQuestion();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.FocusInput();
}