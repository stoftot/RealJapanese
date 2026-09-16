using Android.Views;
using AndroidX.Core.View;

namespace RealJapanese.Mobile;

/// <summary>Passes actual IME visibility to the local page, including Back dismissal.</summary>
internal sealed class KeyboardNavigationObserver : IDisposable
{
    private readonly Android.Webkit.WebView webView;
    private readonly ViewTreeObserver? observer;
    private bool? keyboardVisible;

    public KeyboardNavigationObserver(Android.Webkit.WebView webView)
    {
        this.webView = webView;
        observer = webView.ViewTreeObserver;
        // Observe layout without replacing MAUI's window-insets listener.
        if (observer is not null)
            observer.GlobalLayout += OnGlobalLayout;
    }

    private void OnGlobalLayout(object? sender, EventArgs e)
    {
        var insets = ViewCompat.GetRootWindowInsets(webView);
        if (insets is null) return;
        var visible = insets.IsVisible(WindowInsetsCompat.Type.Ime());
        if (visible == keyboardVisible) return;
        keyboardVisible = visible;
        webView.EvaluateJavascript(
            $"document.documentElement.classList.toggle('android-keyboard-open', {(visible ? "true" : "false")});",
            null);
    }

    public void Dispose()
    {
        if (observer?.IsAlive == true)
            observer.GlobalLayout -= OnGlobalLayout;
    }
}
