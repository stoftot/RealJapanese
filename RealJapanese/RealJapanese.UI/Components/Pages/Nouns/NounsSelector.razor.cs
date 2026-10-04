using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Pages.Nouns;

public class NounsSelectorBase : WordComponentBase<Word>
{
    [Inject] private NounData NounDataInjected { get; set; } = null!;
    protected override WordDataBase<Word> WordData => NounDataInjected;
}
