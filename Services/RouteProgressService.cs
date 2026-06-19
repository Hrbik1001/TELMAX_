using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace PIDMobileSpeaker.Services;

public sealed class RouteProgressService
{
    private readonly AudioService _audio;
    private readonly AppDataService _data;

    private readonly PhrasesFile _phrases;
    private CancellationTokenSource? _gpsCts;

    private bool _entered80;
    private bool _announced50;
    private bool _announcedDeparture80;

    public CachedTrip? Trip => _data.SelectedTrip;
    public int StopIndex { get; private set; }
    public Location? LastLocation { get; private set; }
    public double DistanceToCurrentStopM { get; private set; }
    public string TrackingState { get; private set; } = "Vypnuto";
    public bool IsTracking => _gpsCts != null;

    public event Action? StateChanged;

    public RouteProgressService(AudioService audio, AppDataService data)
    {
        _audio = audio;
        _data = data;
        _phrases = JsonStore.Load(Paths.PhrasesFile, new PhrasesFile());
    }

    public void AttachTrip(CachedTrip trip)
    {
        _data.SelectTrip(trip);
        StopIndex = 0;
        ResetStopFlags();
        TrackingState = "Spoj načten";
        StateChanged?.Invoke();
    }

    public CachedTripStop? CurrentStop =>
        Trip?.Stops.Count > 0 ? Trip.Stops[Math.Clamp(StopIndex, 0, Trip.Stops.Count - 1)] : null;

    public CachedTripStop? NextStop =>
        Trip != null && StopIndex + 1 < Trip.Stops.Count ? Trip.Stops[StopIndex + 1] : null;

    public async Task StartTrackingAsync()
    {
        if (Trip == null)
        {
            TrackingState = "Nejdřív načti spoj.";
            StateChanged?.Invoke();
            return;
        }

        if (_gpsCts != null) return;

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            TrackingState = "GPS oprávnění zamítnuto.";
            StateChanged?.Invoke();
            return;
        }

        _gpsCts = new CancellationTokenSource();
        TrackingState = "GPS běží";
        StateChanged?.Invoke();
        _ = Task.Run(() => GpsLoopAsync(_gpsCts.Token));
    }

    public void StopTracking()
    {
        _gpsCts?.Cancel();
        _gpsCts = null;
        TrackingState = "GPS vypnuto";
        StateChanged?.Invoke();
    }

    private async Task GpsLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(3));
                var location = await Geolocation.Default.GetLocationAsync(request, token);
                if (location != null)
                    await ProcessLocationAsync(location, token);
            }
            catch (Exception ex)
            {
                Logger.Error("GPS chyba: " + ex.Message);
                TrackingState = "GPS chyba: " + ex.Message;
                StateChanged?.Invoke();
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task ProcessLocationAsync(Location location, CancellationToken token)
    {
        LastLocation = location;

        var stop = CurrentStop;
        if (Trip == null || stop == null)
        {
            StateChanged?.Invoke();
            return;
        }

        DistanceToCurrentStopM = DistanceMeters(location.Latitude, location.Longitude, stop.Lat, stop.Lon);

        if (DistanceToCurrentStopM <= 80)
        {
            _entered80 = true;
            TrackingState = "Uvnitř 80 m pole";
        }

        if (DistanceToCurrentStopM <= 50 && !_announced50)
        {
            _announced50 = true;
            TrackingState = "Vstup 50 m → hlášení zastávky";
            StateChanged?.Invoke();
            await AnnounceCurrentStopAsync(token);
        }

        if (_entered80 && DistanceToCurrentStopM > 80 && !_announcedDeparture80)
        {
            _announcedDeparture80 = true;
            TrackingState = "Opuštění 80 m pole → příští zastávka";
            StateChanged?.Invoke();
            await MoveToNextAndAnnounceAsync(token);
        }

        StateChanged?.Invoke();
    }

    public async Task ManualCurrentAsync()
    {
        if (CurrentStop == null) return;
        _announced50 = true;
        TrackingState = "Ruční hlášení zastávky";
        StateChanged?.Invoke();
        await AnnounceCurrentStopAsync(CancellationToken.None);
    }

    public async Task ManualNextAsync()
    {
        TrackingState = "Ruční příští zastávka";
        StateChanged?.Invoke();
        await MoveToNextAndAnnounceAsync(CancellationToken.None);
    }

    public void MovePrevious()
    {
        if (Trip == null) return;
        StopIndex = Math.Max(0, StopIndex - 1);
        ResetStopFlags();
        TrackingState = "Posun zpět";
        StateChanged?.Invoke();
    }

    public void MoveForwardWithoutAnnouncement()
    {
        if (Trip == null) return;
        StopIndex = Math.Min(Trip.Stops.Count - 1, StopIndex + 1);
        ResetStopFlags();
        TrackingState = "Posun vpřed";
        StateChanged?.Invoke();
    }

    private async Task MoveToNextAndAnnounceAsync(CancellationToken token)
    {
        if (Trip == null) return;

        if (StopIndex + 1 >= Trip.Stops.Count)
        {
            TrackingState = "Konec trasy";
            StateChanged?.Invoke();
            return;
        }

        StopIndex++;
        ResetStopFlags();

        var stop = CurrentStop;
        if (stop == null) return;

        await AnnounceNextStopAsync(stop, token);
        StateChanged?.Invoke();
    }

    private void ResetStopFlags()
    {
        _entered80 = false;
        _announced50 = false;
        _announcedDeparture80 = false;
        DistanceToCurrentStopM = 0;
    }

    private async Task AnnounceCurrentStopAsync(CancellationToken token)
    {
        var stop = CurrentStop;
        if (stop == null) return;

        var files = new List<string> { Phrase("GONG"), StopAudioFile(stop) };

        if (Trip != null && StopIndex == Trip.Stops.Count - 1)
        {
            files.Add(Phrase("TERMINAL_1"));
            files.Add(Phrase("TERMINAL_2"));
            files.Add(Phrase("TERMINAL_EN"));
        }

        Logger.Info($"50 m: hlášení zastávky {stop.Name} CIS {stop.Cis}");
        await _audio.PlaySequenceAsync(files, token);
    }

    private async Task AnnounceNextStopAsync(CachedTripStop stop, CancellationToken token)
    {
        var files = new List<string>
        {
            Phrase("GONG_B"),
            Phrase("NEXT_STOP"),
            StopAudioFile(stop)
        };

        Logger.Info($"Opuštění 80 m: příští zastávka {stop.Name} CIS {stop.Cis}");
        await _audio.PlaySequenceAsync(files, token);
    }

    private string Phrase(string key)
    {
        if (_phrases.Phrases.TryGetValue(key, out var path) && !string.IsNullOrWhiteSpace(path))
            return path;

        return key switch
        {
            "GONG" => "audio/System/GONG.mp3",
            "GONG_B" => "audio/System/Gong_b.mp3",
            "NEXT_STOP" => "audio/System/Příští zastávka.mp3",
            "TERMINAL_1" => "audio/System/H113.mp3",
            "TERMINAL_2" => "audio/System/H114.mp3",
            "TERMINAL_EN" => "audio/System/AJkonec.mp3",
            _ => "audio/System/" + key + ".mp3"
        };
    }

    private static string StopAudioFile(CachedTripStop stop)
    {
        if (!string.IsNullOrWhiteSpace(stop.AudioFile))
            return stop.AudioFile;

        if (!string.IsNullOrWhiteSpace(stop.Cis))
            return "audio/Zastavky/" + stop.Cis + ".mp3";

        return "";
    }

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        => Geo.DistanceKm(lat1, lon1, lat2, lon2) * 1000.0;
}
