using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Pages.Adjectives;

public class AdjectiveSelectorBase : WordComponentBase<Adjective>
{
    [Inject] private AdjectiveData WordDataInjected { get; set; } = null!;
    protected override WordDataBase<Adjective> WordData => WordDataInjected;
}