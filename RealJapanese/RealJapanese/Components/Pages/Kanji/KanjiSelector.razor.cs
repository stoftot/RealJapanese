using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Pages.Kanji;

public class KanjiSelectorBase : WordComponentBase<Word>
{
    private bool singleKanjiMode = false;

    protected bool SingleKanjiMode
    {
        get => singleKanjiMode;
        set
        {
            if (singleKanjiMode == value) return;
            singleKanjiMode = value;
            OnInitialized();
        }
    }
    
    [Inject] private KanjiData KanjiDataInjected { get; set; } = null!;
    protected override WordDataBase<Word> WordData => 
        SingleKanjiMode ? KanjiDataInjected.Single : KanjiDataInjected.Combined;

    public List<PraticeSelectorBase.PageDescreption> PageDescreptions()
    {
        var pagesDescreptions = new List<PraticeSelectorBase.PageDescreption>();

        if (SingleKanjiMode)
        {
            pagesDescreptions.Add(
                new()
                {
                    Title = "Learn the kanji",
                    Description = "Practice the meaning of the individual kanji",
                    Url = $"single/meaning?training={Training}",
                }
            );
        }
        else
        {
            pagesDescreptions.Add(
                new()
                {
                    Title = "Learn the kanji",
                    Description = "Practice then meaning of the kanji in words",
                    Url = $"combined/meaning?training={Training}",
                }
            );
        }
        return pagesDescreptions;
    }
        
}