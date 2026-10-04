using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RealJapanese.TestSupport;
using Repositories;

namespace RealJapanese.ComponentTests;

/// <summary>Creates an isolated bUnit renderer, catalog copy, progress root, and service graph for one test.</summary>
internal sealed class ComponentTestContext : IDisposable
{
    public TestWorkspace Workspace { get; } = new();
    public BunitContext Context { get; } = new();
    public RepositoryPaths Paths { get; }
    public WordData Words { get; }
    public NounData Nouns { get; }
    public VerbData Verbs { get; }
    public AdjectiveData Adjectives { get; }
    public KanjiData Kanji { get; }

    public ComponentTestContext()
    {
        Paths = Workspace.CreatePaths();
        Words = new WordData(Paths);
        Nouns = new NounData(Paths);
        Verbs = new VerbData(Paths);
        Adjectives = new AdjectiveData(Paths);
        Kanji = new KanjiData(Paths);

        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.Services.AddSingleton(Paths);
        Context.Services.AddSingleton(Words);
        Context.Services.AddSingleton(Nouns);
        Context.Services.AddSingleton(Verbs);
        Context.Services.AddSingleton(Adjectives);
        Context.Services.AddSingleton(Kanji);
        Context.Services.AddSingleton<NumbersQuestionGenerator>();
    }

    public void UseCategory(string category) =>
        Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/?category={category}");

    public void Dispose()
    {
        Context.Dispose();
        Workspace.Dispose();
    }
}
