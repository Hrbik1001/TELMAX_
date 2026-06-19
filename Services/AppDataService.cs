using System.IO.Compression;

namespace PIDMobileSpeaker.Services;

public sealed class AppDataService
{
    public AppCache? Cache { get; private set; }
    public CachedTrip? SelectedTrip { get; private set; }

    public bool HasData =>
        Directory.Exists(Paths.Pid) ||
        File.Exists(Path.Combine(Paths.Data, "PID.zip")) ||
        File.Exists(Paths.StopsByNameFile);

    public async Task<string> ImportZipAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Vyber ZIP s data a nahrávkami"
        });

        if (result == null)
            return "Import zrušen.";

        Paths.EnsureFolders();

        var tempZip = Path.Combine(FileSystem.CacheDirectory, "pid_mobile_import.zip");
        await using (var input = await result.OpenReadAsync())
        await using (var output = File.Create(tempZip))
        {
            await input.CopyToAsync(output);
        }

        ZipFile.ExtractToDirectory(tempZip, Paths.Root, overwriteFiles: true);
        NormalizeExtractedLayout();

        Logger.Info("Import ZIP hotový: " + result.FileName);
        return "ZIP importován do: " + Paths.Root;
    }

    private static void NormalizeExtractedLayout()
    {
        // Když někdo zazipuje celou složku PIDSpeaker, přesuneme její data/audio nahoru.
        // Lidé zipují věci všelijak. Počítače pak trpí. Tady je náplast.
        var nested = Path.Combine(Paths.Root, "PIDSpeaker");
        if (!Directory.Exists(nested)) return;

        MoveIfExists(Path.Combine(nested, "data"), Paths.Data);
        MoveIfExists(Path.Combine(nested, "Audio"), Path.Combine(Paths.Root, "Audio"));
        MoveIfExists(Path.Combine(nested, "audio"), Path.Combine(Paths.Root, "audio"));
    }

    private static void MoveIfExists(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var dst = Path.Combine(target, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(file, dst, overwrite: true);
        }
    }

    public async Task<AppCache> LoadDataAsync()
    {
        return await Task.Run(() =>
        {
            Paths.EnsureFolders();
            Cache = new ImportService().LoadOrImport();
            Logger.Info($"Data načtena: linky={Cache.RoutesById.Count}, spoje={Cache.TripsById.Count}");
            return Cache;
        });
    }

    public List<CachedTrip> FindTrips(string line, string spoj, int limit = 80)
    {
        if (Cache == null) return new();

        var trips = Cache.TripsById.Values
            .Where(t => string.IsNullOrWhiteSpace(line) || t.Line.Equals(line.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(t =>
                string.IsNullOrWhiteSpace(spoj) ||
                t.TripShortName.Equals(spoj.Trim(), StringComparison.OrdinalIgnoreCase) ||
                t.TripId.Contains(spoj.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Stops.Count >= 2)
            .OrderBy(t => t.Stops.FirstOrDefault()?.DepartureTime ?? "99:99:99")
            .Take(limit)
            .ToList();

        return trips;
    }

    public void SelectTrip(CachedTrip trip)
    {
        SelectedTrip = trip;
        Logger.Info($"Vybrán spoj: linka {trip.Line}, trip {trip.TripId}, spoj {trip.TripShortName}");
    }
}
