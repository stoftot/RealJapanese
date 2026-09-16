using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Pages.Verbs;

public class VerbSelectorBase : WordComponentBase<Verb>
{
    [Inject] private VerbData WordDataInjected { get; set; } = null!;
    protected override WordDataBase<Verb> WordData => WordDataInjected;
}