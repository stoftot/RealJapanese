namespace RealJapanese.Mobile;

public partial class App : Application
{
    private readonly MainPage mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        this.mainPage = mainPage;
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(mainPage) { Title = "RealJapanese" };
}
