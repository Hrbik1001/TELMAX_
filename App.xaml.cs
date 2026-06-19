namespace PIDMobileSpeaker;

public partial class App : Application
{
    public App(MainPage page)
    {
        InitializeComponent();

        UserAppTheme = AppTheme.Dark;
        MainPage = new NavigationPage(page)
        {
            BarBackgroundColor = Color.FromArgb("#111820"),
            BarTextColor = Colors.White
        };
    }
}
