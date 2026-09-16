using Microsoft.AspNetCore.Components.WebView.Maui;

namespace RealJapanese.Mobile;

public partial class MainPage : ContentPage
{
    private readonly StudyDataInstaller installer;
    private BlazorWebView? webView;
    private bool initializing;
    private KeyboardNavigationObserver? keyboardObserver;

    public MainPage(StudyDataInstaller installer)
    {
        InitializeComponent();
        this.installer = installer;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeAsync();
    }

    private async void RetryClicked(object? sender, EventArgs e) => await InitializeAsync();

    private async Task InitializeAsync()
    {
        if (webView is not null || initializing) return;
        initializing = true;
        Spinner.IsRunning = true;
        Retry.IsVisible = false;
        Status.Text = "Preparing your offline vocabulary…";
        try
        {
            await installer.InstallAsync();
            var view = new BlazorWebView { HostPage = "wwwroot/index.html" };
            view.HandlerChanging += (_, _) =>
            {
                keyboardObserver?.Dispose();
                keyboardObserver = null;
            };
            view.HandlerChanged += (_, _) =>
            {
                if (view.Handler?.PlatformView is Android.Webkit.WebView nativeView)
                    keyboardObserver = new KeyboardNavigationObserver(nativeView);
            };
            view.RootComponents.Add(new RootComponent
            {
                Selector = "#app", ComponentType = typeof(RealJapanese.Components.Routes)
            });
            Root.Children.Add(view);
            webView = view;
            Loading.IsVisible = false;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            Status.Text = "Your study files could not be opened. Please try again. Your saved progress has not been reset.";
            Retry.IsVisible = true;
        }
        finally
        {
            initializing = false;
            Spinner.IsRunning = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (webView?.Handler?.PlatformView is Android.Webkit.WebView nativeView && nativeView.CanGoBack())
        {
            nativeView.GoBack();
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
