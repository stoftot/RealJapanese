using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace RealJapanese.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e?.KeyCode != Keycode.Back)
            return base.DispatchKeyEvent(e);

        // BlazorWebView handles Back on key-down while MAUI also handles key-up.
        // Route the complete key press through MAUI once, like a Back gesture.
        // The IME gets its dismissal opportunity before activity key dispatch.
        if (e.Action == KeyEventActions.Up && !e.IsCanceled)
        {
#pragma warning disable CS0612, CS0618 // MAUI's entry point also handles predictive Back.
            OnBackPressed();
#pragma warning restore CS0612, CS0618
        }

        return true;
    }
}
