namespace PIDMobileSpeaker;

public sealed class MainPage : ContentPage
{
    private readonly Label _displayLine = Text("544p", 42, true, Colors.White);
    private readonly Label _displayTrip = Text("Spoj --", 24, true, Colors.White);
    private readonly Label _displayStop = Text("Aktuální zastávka", 28, true, Colors.White);
    private readonly Label _displayNext = Text("Příští zastávka", 24, true, Color.FromArgb("#BFD7EA"));
    private readonly Label _status = Text("TELMAX Mobile spuštěn", 18, false, Color.FromArgb("#DCE7EF"));

    public MainPage()
    {
        Title = "TELMAX Mobile";
        BackgroundColor = Color.FromArgb("#88A2B2");
        Content = BuildScreen();
    }

    private View BuildScreen()
    {
        var root = new Grid
        {
            Padding = new Thickness(6),
            RowSpacing = 6,
            ColumnSpacing = 6,
            RowDefinitions =
            {
                new RowDefinition(new GridLength(54)),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(64))
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(245))
            }
        };

        var header = new Grid
        {
            BackgroundColor = Color.FromArgb("#AEB9BE"),
            Padding = new Thickness(10, 4),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(260))
            }
        };
        header.Add(Text("Hořovice, Žel. stanice", 22, true, Colors.Black), 0, 0);
        header.Add(Text("Výdej jízdenek", 22, true, Color.FromArgb("#004E9E")), 1, 0);
        root.Add(header, 0, 0);
        Grid.SetColumnSpan(header, 2);

        var main = new Grid
        {
            RowSpacing = 6,
            ColumnSpacing = 6,
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(1, GridUnitType.Star))
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            }
        };

        AddButton(main, "Linka / spoj", "#0169B4", () => SetStatus("Výběr linky/spoje zatím připraven jako obrazovka."), 0, 0);
        AddButton(main, "Výdej\njízdenek", "#38A38C", () => SetStatus("Výdej jízdenek bude další přepsaná obrazovka."), 1, 0);
        AddButton(main, "Služební\nhlášky", "#B4ABD4", () => SetStatus("Služební hlášky budou napojené na audio."), 2, 0);
        AddButton(main, "Údaje\npro ČK", "#96AFBD", () => SetStatus("Údaje pro ČK připraveno."), 3, 0);

        AddButton(main, "Hlásit\nzastávku", "#619BC4", () => SetStatus("Ruční hlášení zastávky."), 0, 1);
        AddButton(main, "Příští\nzastávka", "#619BC4", () => SetStatus("Ruční příští zastávka."), 1, 1);
        AddButton(main, "Start\nGPS", "#898A37", () => SetStatus("GPS režim: 50 m / 80 m bude napojený v další verzi."), 2, 1);
        AddButton(main, "Stop\nGPS", "#8A9DA2", () => SetStatus("GPS zastaveno."), 3, 1);

        AddButton(main, "544p", "#84873D", () => SetLine("544p"), 0, 2);
        AddButton(main, "544z", "#84873D", () => SetLine("544z"), 1, 2);
        AddButton(main, "Spoj 1", "#AEB9BE", () => SetTrip("Spoj 1"), 2, 2);
        AddButton(main, "Spoj 3", "#AEB9BE", () => SetTrip("Spoj 3"), 3, 2);

        AddButton(main, "←", "#AEB9BE", () => SetStatus("Posun zpět."), 0, 3);
        AddButton(main, "→", "#AEB9BE", () => SetStatus("Posun vpřed."), 1, 3);
        AddButton(main, "Import\ndat", "#96AFBD", () => SetStatus("Import dat bude napojený později."), 2, 3);
        AddButton(main, "Nastavení", "#96AFBD", () => SetStatus("Nastavení."), 3, 3);

        root.Add(main, 0, 1);

        var right = new VerticalStackLayout
        {
            Spacing = 6,
            BackgroundColor = Color.FromArgb("#9AA9AE"),
            Padding = new Thickness(6),
            Children =
            {
                SideButton("Ztmavení\ndispleje"),
                SideButton("Storno\njízdenky"),
                SideButton("Lítačka"),
                SideButton("Online\ndotaz"),
                SideButton("ARRIVA"),
                SideButton("Zavazadlo\nPes")
            }
        };
        root.Add(right, 1, 1);

        var bottom = new Grid
        {
            BackgroundColor = Color.FromArgb("#151D2E"),
            Padding = new Thickness(10, 4),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(120)),
                new ColumnDefinition(new GridLength(120)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(300))
            }
        };
        bottom.Add(_displayLine, 0, 0);
        bottom.Add(_displayTrip, 1, 0);
        bottom.Add(new VerticalStackLayout
        {
            Spacing = 0,
            Children = { _displayStop, _displayNext }
        }, 2, 0);
        bottom.Add(_status, 3, 0);
        root.Add(bottom, 0, 2);
        Grid.SetColumnSpan(bottom, 2);

        return root;
    }

    private void SetLine(string line)
    {
        _displayLine.Text = line;
        SetStatus("Vybrána linka " + line);
    }

    private void SetTrip(string trip)
    {
        _displayTrip.Text = trip;
        SetStatus("Vybrán " + trip);
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
    }

    private static void AddButton(Grid grid, string text, string color, Action action, int column, int row)
    {
        var button = TelmaxButton(text, Color.FromArgb(color));
        button.Clicked += (_, _) => action();
        grid.Add(button, column, row);
    }

    private static Button SideButton(string text)
    {
        return TelmaxButton(text, Color.FromArgb("#AEB9BE"));
    }

    private static Button TelmaxButton(string text, Color color)
    {
        return new Button
        {
            Text = text,
            TextColor = Colors.Black,
            BackgroundColor = color,
            BorderColor = Color.FromArgb("#48555B"),
            BorderWidth = 1,
            CornerRadius = 0,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(4)
        };
    }

    private static Label Text(string text, double size, bool bold, Color color)
    {
        return new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            TextColor = color,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
    }
}
