using PIDMobileSpeaker.Services;

namespace PIDMobileSpeaker;

public sealed class MainPage : ContentPage
{
    private readonly AppDataService _data;
    private readonly RouteProgressService _route;

    private readonly Entry _lineEntry = new() { Placeholder = "Linka", Keyboard = Keyboard.Numeric };
    private readonly Entry _spojEntry = new() { Placeholder = "Spoj / trip", Keyboard = Keyboard.Text };
    private readonly Picker _tripPicker = new() { Title = "Vyber spoj" };

    private readonly Label _status = Label("Start");
    private readonly Label _line = BigLabel("-");
    private readonly Label _trip = BigLabel("-");
    private readonly Label _current = BigLabel("Aktuální: -");
    private readonly Label _next = BigLabel("Příští: -");
    private readonly Label _distance = BigLabel("GPS: -");
    private readonly Label _gps = Label("-");
    private readonly Label _log = Label("");

    private readonly List<CachedTrip> _foundTrips = new();

    public MainPage(AppDataService data, RouteProgressService route)
    {
        _data = data;
        _route = route;
        _route.StateChanged += RefreshState;
        Logger.LineWritten += line => MainThread.BeginInvokeOnMainThread(() => _log.Text = line);

        Title = "PID Mobile Speaker";
        BackgroundColor = Color.FromArgb("#1F272F");

        Content = BuildLayout();
        RefreshState();
    }

    private View BuildLayout()
    {
        var root = new Grid
        {
            Padding = new Thickness(12),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star)
            }
        };

        var top = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(2.0, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        _lineEntry.TextColor = Colors.White;
        _spojEntry.TextColor = Colors.White;
        _lineEntry.BackgroundColor = Color.FromArgb("#2B3440");
        _spojEntry.BackgroundColor = Color.FromArgb("#2B3440");
        _tripPicker.TextColor = Colors.White;
        _tripPicker.BackgroundColor = Color.FromArgb("#2B3440");

        top.Add(_lineEntry, 0, 0);
        top.Add(_spojEntry, 1, 0);
        top.Add(_tripPicker, 2, 0);
        top.Add(Button("Import ZIP", ImportZipAsync), 3, 0);
        top.Add(Button("Načíst data", LoadDataAsync), 4, 0);
        top.Add(Button("Najít spoj", FindTripsAsync), 5, 0);

        root.Add(top, 0, 0);

        var body = new Grid
        {
            Margin = new Thickness(0, 12, 0, 12),
            ColumnSpacing = 12,
            RowSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.8, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star))
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        var leftPanel = Panel(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Label("Linka"),
                _line,
                Label("Spoj"),
                _trip,
                Label("Stav"),
                _status
            }
        });

        var centerPanel = Panel(new VerticalStackLayout
        {
            Spacing = 14,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _current,
                _next,
                _distance,
                _gps
            }
        });

        var rightPanel = Panel(new Grid
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Star)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            Children =
            {
                Button("Start GPS", StartGpsAsync).At(0, 0),
                Button("Stop", StopGps).At(1, 0),
                Button("Hlásit zastávku\n50 m", ManualCurrentAsync).At(0, 1),
                Button("Příští zastávka\npo 80 m", ManualNextAsync).At(1, 1),
                Button("←", () => { _route.MovePrevious(); return Task.CompletedTask; }).At(0, 2),
                Button("→", () => { _route.MoveForwardWithoutAnnouncement(); return Task.CompletedTask; }).At(1, 2),
                Button("Vybrat spoj", SelectTripAsync).At(0, 3, 2)
            }
        });

        body.Add(leftPanel, 0, 0);
        Grid.SetRowSpan(leftPanel, 3);

        body.Add(centerPanel, 1, 0);
        Grid.SetRowSpan(centerPanel, 3);

        body.Add(rightPanel, 2, 0);
        Grid.SetRowSpan(rightPanel, 3);

        root.Add(body, 0, 1);

        _log.LineBreakMode = LineBreakMode.TailTruncation;
        _log.TextColor = Color.FromArgb("#BDBEBF");
        root.Add(_log, 0, 2);

        return root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_data.HasData && _data.Cache == null)
        {
            _status.Text = "Data nalezena. Klikni Načíst data.";
        }

        await DisplayAlert(
            "GPS hlášení",
            "Logika: vstup do 50 m pole okolo aktuální zastávky = Hlášení zastávky. Opuštění 80 m pole = posun na další zastávku a hlášení Příští zastávka. Ano, konečně nějaká logika, která se dá vysvětlit bez tabule.",
            "OK");
    }

    private async Task ImportZipAsync()
    {
        try
        {
            _status.Text = "Importuju ZIP...";
            var msg = await _data.ImportZipAsync();
            _status.Text = msg;
        }
        catch (Exception ex)
        {
            _status.Text = "Import selhal: " + ex.Message;
            Logger.Error(ex.ToString());
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _status.Text = "Načítám PID data...";
            await _data.LoadDataAsync();
            _status.Text = "Data načtena. Zadej linku/spoj.";
        }
        catch (Exception ex)
        {
            _status.Text = "Načtení selhalo: " + ex.Message;
            Logger.Error(ex.ToString());
        }
    }

    private Task FindTripsAsync()
    {
        _foundTrips.Clear();
        _tripPicker.Items.Clear();

        var trips = _data.FindTrips(_lineEntry.Text ?? "", _spojEntry.Text ?? "");
        foreach (var t in trips)
        {
            _foundTrips.Add(t);
            var first = t.Stops.FirstOrDefault();
            var last = t.Stops.LastOrDefault();
            _tripPicker.Items.Add($"{t.Line} / {ShortTrip(t)}  {first?.DepartureTime}  {first?.Name} → {last?.Name}");
        }

        if (_tripPicker.Items.Count > 0)
        {
            _tripPicker.SelectedIndex = 0;
            _status.Text = $"Nalezeno {_tripPicker.Items.Count} spojů.";
        }
        else
        {
            _status.Text = "Nic nenalezeno. To je buď špatná linka, nebo realita znovu škodí.";
        }

        return Task.CompletedTask;
    }

    private Task SelectTripAsync()
    {
        if (_tripPicker.SelectedIndex < 0 || _tripPicker.SelectedIndex >= _foundTrips.Count)
        {
            _status.Text = "Vyber spoj v seznamu.";
            return Task.CompletedTask;
        }

        _route.AttachTrip(_foundTrips[_tripPicker.SelectedIndex]);
        RefreshState();
        return Task.CompletedTask;
    }

    private async Task StartGpsAsync()
    {
        await _route.StartTrackingAsync();
        RefreshState();
    }

    private Task StopGps()
    {
        _route.StopTracking();
        RefreshState();
        return Task.CompletedTask;
    }

    private async Task ManualCurrentAsync()
    {
        await _route.ManualCurrentAsync();
        RefreshState();
    }

    private async Task ManualNextAsync()
    {
        await _route.ManualNextAsync();
        RefreshState();
    }

    private void RefreshState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var trip = _route.Trip;
            var cur = _route.CurrentStop;
            var next = _route.NextStop;
            var loc = _route.LastLocation;

            _line.Text = trip?.Line ?? "-";
            _trip.Text = trip == null ? "-" : ShortTrip(trip);
            _current.Text = cur == null ? "Aktuální: -" : $"Aktuální: {cur.Sequence}. {cur.Name}";
            _next.Text = next == null ? "Příští: -" : $"Příští: {next.Sequence}. {next.Name}";
            _status.Text = _route.TrackingState;

            if (cur != null && loc != null)
            {
                _distance.Text = $"Vzdálenost k aktuální: {_route.DistanceToCurrentStopM:0} m";
                _gps.Text = $"GPS: {loc.Latitude:0.000000}, {loc.Longitude:0.000000}  přesnost {loc.Accuracy:0} m  rychlost {(loc.Speed ?? 0) * 3.6:0.0} km/h";
            }
            else
            {
                _distance.Text = "Vzdálenost k aktuální: -";
                _gps.Text = "GPS: -";
            }
        });
    }

    private static string ShortTrip(CachedTrip trip)
        => !string.IsNullOrWhiteSpace(trip.TripShortName) ? trip.TripShortName : trip.TripId;

    private static Label Label(string text) => new()
    {
        Text = text,
        TextColor = Color.FromArgb("#BDBEBF"),
        FontSize = 18,
        FontAttributes = FontAttributes.Bold
    };

    private static Label BigLabel(string text) => new()
    {
        Text = text,
        TextColor = Colors.White,
        FontSize = 30,
        FontAttributes = FontAttributes.Bold,
        LineBreakMode = LineBreakMode.TailTruncation
    };

    private static Border Panel(View content) => new()
    {
        Stroke = Color.FromArgb("#34404C"),
        StrokeThickness = 1,
        Background = Color.FromArgb("#151D2E"),
        Padding = new Thickness(14),
        Content = content
    };

    private static Button Button(string text, Func<Task> click)
    {
        var b = new Button
        {
            Text = text,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#263241"),
            BorderColor = Color.FromArgb("#4B5A68"),
            BorderWidth = 1,
            CornerRadius = 0,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        };

        b.Clicked += async (_, _) =>
        {
            try { await click(); }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        };

        return b;
    }
}

public static class GridChildExtensions
{
    public static View At(this View view, int column, int row, int columnSpan = 1, int rowSpan = 1)
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
        Grid.SetColumnSpan(view, columnSpan);
        Grid.SetRowSpan(view, rowSpan);
        return view;
    }
}
