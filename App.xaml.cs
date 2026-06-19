namespace PIDMobileSpeaker;

public partial class App : Application
{
    private readonly MainPage _page;

    public App(MainPage page)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
        _page = page;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var navigationPage = new NavigationPage(_page)
        {
            BarBackgroundColor = Color.FromArgb("#111820"),
            BarTextColor = Colors.White
        };

        return new Window(navigationPage);
    }
}
