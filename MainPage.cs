using PIDMobileSpeaker.Services;

namespace PIDMobileSpeaker;

public sealed class MainPage : ContentPage
{
    private readonly AppDataService _data;
    private readonly RouteProgressService _route;

    private readonly Entry _lineEntry = new() { Placeholder = "Linka", Keyboard = Keyboard.Numeric };
    private readonly Entry _spojEntry = new() { Placeholder = "Spoj", Keyboard = Keyboard.Text };
    private readonly Picker _tripPicker = new() { Title = "Vyber spoj" };

    private readonly Label _status = MakeSmallLabel("Start");
    private readonly Label _line = MakeBigLabel("-");
    private readonly Label _trip = MakeBigLabel("-");
    private readonly Label _current = MakeBigLabel("Aktuální: -");
    private readonly Label _next = MakeBigLabel("Příští: -");
    private readonly Label _distance = MakeBigLabel("Vzdálenost: -");
    private readonly Label _gps = MakeSmallLabel("GPS: -");
    private readonly Label _log = MakeSmallLabel("");

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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_data.HasData && _data.Cache == null)
            _status.Text = "Data nalezena. Klikni Načíst data.";
    }

    private View BuildLayout()
    {
        _lineEntry.TextColor = Colors.White;
        _lineEntry.BackgroundColor = Color.FromArgb("#2B3440");
        _spojEntry.TextColor = Colors.White;
        _spojEntry.BackgroundColor = Color.FromArgb("#2B3440");
        _tripPicker.TextColor = Colors.White;
        _tripPicker.BackgroundColor = Color.FromArgb("#2B3440");

        var top = new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Sized(_lineEntry, 120),
                Sized(_spojEntry, 120),
                Sized(_tripPicker, 420),
                Button("Import ZIP", ImportZipAsync),
                Button("Načíst data", LoadDataAsync),
                Button("Najít spoj", FindTripsAsync)
            }
        };

        var left = Panel(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                MakeSmallLabel("Linka"), _line,
                MakeSmallLabel("Spoj"), _trip,
                MakeSmallLabel("Stav"), _status
            }
        });

        var middle = Panel(new VerticalStackLayout
        {
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            Children = { _current, _next, _distance, _gps }
        });

        var buttons = new Grid
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
            }
        };

        AddToGrid(buttons, Button("Start GPS", StartGpsAsync), 0, 0);
        AddToGrid(buttons, Button("Stop", StopGps), 1, 0);
        AddToGrid(buttons, Button("Hlásit zastávku\n50 m", ManualCurrentAsync), 0, 1);
        AddToGrid(buttons, Button("Příští zastávka\npo 80 m", ManualNextAsync), 1, 1);
        AddToGrid(buttons, Button("←", () => { _route.MovePrevious(); return Task.CompletedTask; }), 0, 2);
        AddToGrid(buttons, Button("→", () => { _route.MoveForwardWithoutAnnouncement(); return Task.CompletedTask; }), 1, 2);
        AddToGrid(buttons, Button("Vybrat spoj", SelectTripAsync), 0, 3, 2);

        var right = Panel(buttons);

        var body = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.8, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star))
            }
        };

        body.Add(left, 0, 0);
        body.Add(middle, 1, 0);
        body.Add(right, 2, 0);

        var root = new VerticalStackLayout
        {
            Padding = new Thickness(12),
            Spacing = 12,
            Children = { top, body, _log }
        };

        return root;
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

        foreach (var trip in _data.FindTrips(_lineEntry.Text ?? "", _spojEntry.Text ?? ""))
        {
            _foundTrips.Add(trip);
            var first = trip.Stops.FirstOrDefault();
            var last = trip.Stops.LastOrDefault();
            _tripPicker.Items.Add($"{trip.Line} / {ShortTrip(trip)}  {first?.DepartureTime}  {first?.Name} → {last?.Name}");
        }

        if (_tripPicker.Items.Count > 0)
        {
            _tripPicker.SelectedIndex = 0;
            _status.Text = $"Nalezeno {_tripPicker.Items.Count} spojů.";
        }
        else
        {
            _status.Text = "Nic nenalezeno.";
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

    private static View Sized(View view, double width)
    {
        view.WidthRequest = width;
        return view;
    }

    private static void AddToGrid(Grid grid, View view, int column, int row, int columnSpan = 1)
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
        Grid.SetColumnSpan(view, columnSpan);
        grid.Children.Add(view);
    }

    private static Label MakeSmallLabel(string text) => new()
    {
        Text = text,
        TextColor = Color.FromArgb("#BDBEBF"),
        FontSize = 18,
        FontAttributes = FontAttributes.Bold
    };

    private static Label MakeBigLabel(string text) => new()
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
        var button = new Button
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

        button.Clicked += async (_, _) =>
        {
            try { await click(); }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        };

        return button;
    }
}
