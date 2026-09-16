using Microsoft.Extensions.Logging;
using Repositories;

namespace RealJapanese.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(new RepositoryPaths(
            Path.Combine(FileSystem.AppDataDirectory, "Catalog"),
            Path.Combine(FileSystem.AppDataDirectory, "Progress")));
        builder.Services.AddSingleton<StudyDataInstaller>();
        builder.Services.AddSingleton<NumbersQuestionGenerator>();
        builder.Services.AddSingleton<WordData>();
        builder.Services.AddSingleton<VerbData>();
        builder.Services.AddSingleton<AdjectiveData>();
        builder.Services.AddSingleton<KanjiData>();
        builder.Services.AddSingleton<Repositories.Sync.ProgressSyncService>();
        builder.Services.AddSingleton<MainPage>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
