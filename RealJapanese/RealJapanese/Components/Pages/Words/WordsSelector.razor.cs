using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Pages.Words;

public class WordsSelectorBase : WordComponentBase<Word>
{
    [Inject] private WordData WordDataInjected { get; set; } = null!;
    protected override WordDataBase<Word> WordData  => WordDataInjected;
}