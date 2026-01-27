using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Pages.Kanji;

public class KanjiSelectorBase : WordComponentBase<Word>
{
    private bool singelKanjiMode = false;

    protected bool SingelKanjiMode
    {
        get => singelKanjiMode;
        set
        {
            if (singelKanjiMode == value) return;
            singelKanjiMode = value;
            OnInitialized();
        }
    }
    
    [Inject] private KanjiData KanjiDataInjected { get; set; } = null!;
    protected override WordDataBase<Word> WordData => 
        SingelKanjiMode ? KanjiDataInjected.Singel : KanjiDataInjected.Combined;

    public List<PraticeSelectorBase.PageDescreption> GetPageDescreptions()
    {
        var pagesDescreptions = new List<PraticeSelectorBase.PageDescreption>();

        if (SingelKanjiMode)
        {
            pagesDescreptions.Add(
                new()
                {
                    Title = "Learn the kanji",
                    Description = "Practice the meaning of the individual kanji",
                    Url = $"meaning?training={Training}",
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
                    Url = $"meaning?training={Training}",
                }
            );
        }
        return pagesDescreptions;
    }
        
}