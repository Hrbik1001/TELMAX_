using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace PIDMobileSpeaker;

public sealed class ImportService
{
    public AppCache LoadOrImport()
    {
        Paths.EnsureFolders();

        // Starší verze ukládala celou cache včetně všech tripů a stop_times do jednoho JSONu.
        // To u PID dat žere paměť jak hladový městský úřad, takže se teď cache na disk neukládá.
        // Import proběhne při startu a na disk se ukládá jen malý report.
        return Import();
    }

    public AppCache Import()
    {
        var cache = new AppCache();
        EnsurePidDataExtracted(cache);
        LoadStopsByName(cache);
        LoadRoutes(cache);
        LoadPidTransferInfo(cache);
        LoadTrips(cache);
        LoadStopTimes(cache);
        BuildTurnuses(cache);
        cache.Report.Routes = cache.RoutesById.Count;
        cache.Report.Trips = cache.TripsById.Count;
        cache.Report.Turnuses = cache.Turnuses.Count;

        // Neschovávat celé AppCache do JSONu. Obsahuje statisíce až miliony položek,
        // a System.Text.Json se na tom umí hezky udusit. Ukládáme jen report.
        JsonStore.Save(Path.Combine(Paths.Cache, "import_report.json"), cache.Report);
        File.WriteAllText(Path.Combine(Paths.Cache, "cache_notice.txt"),
            "Velká cache se záměrně neukládá. Data se importují při startu, aby nedocházelo k OutOfMemoryException.\r\n");
        return cache;
    }

    private static void EnsurePidDataExtracted(AppCache cache)
    {
        var routesPath = Path.Combine(Paths.Pid, "routes.txt");
        var tripsPath = Path.Combine(Paths.Pid, "trips.txt");
        var stopTimesPath = Path.Combine(Paths.Pid, "stop_times.txt");
        if (File.Exists(routesPath) && File.Exists(tripsPath) && File.Exists(stopTimesPath))
            return;

        var zipPath = Path.Combine(Paths.Data, "PID.zip");
        if (!File.Exists(zipPath))
        {
            cache.Report.Warnings.Add("Chybí data/PID.zip a zároveň není rozbalená složka data/PID.");
            return;
        }

        Directory.CreateDirectory(Paths.Data);
        Directory.CreateDirectory(Paths.Pid);

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;

                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("PID/", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(4);

                if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;

                var target = Path.Combine(Paths.Pid, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, true);
            }
            cache.Report.Warnings.Add("PID.zip byl automaticky rozbalen do data/PID.");
        }
        catch (Exception ex)
        {
            cache.Report.Warnings.Add("Automatické rozbalení data/PID.zip selhalo: " + ex.Message);
        }
    }

    private static void LoadStopsByName(AppCache cache)
    {
        if (!File.Exists(Paths.StopsByNameFile))
        {
            cache.Report.Warnings.Add("Chybí data/StopsByName.xml");
            return;
        }

        var doc = XDocument.Load(Paths.StopsByNameFile);
        foreach (var group in doc.Descendants("group"))
        {
            var cis = Attr(group, "cis");
            if (string.IsNullOrWhiteSpace(cis)) continue;

            var stopGroup = new CachedStopGroup
            {
                Cis = cis,
                Name = Attr(group, "name"),
                FullName = Attr(group, "fullName"),
                Node = Attr(group, "node"),
                MainTrafficType = Attr(group, "mainTrafficType"),
                AvgLat = Geo.ParseDouble(Attr(group, "avgLat")),
                AvgLon = Geo.ParseDouble(Attr(group, "avgLon")),
                AudioFile = GuessAudioFile(cis)
            };

            if (IsMetroTraffic(stopGroup.MainTrafficType))
                stopGroup.HasTransferMetro = true;

            foreach (var stop in group.Elements("stop"))
            {
                var gtfsIds = Attr(stop, "gtfsIds").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var firstGtfs = gtfsIds.FirstOrDefault() ?? "";
                var platform = new CachedStop
                {
                    GtfsId = firstGtfs,
                    StopId = Attr(stop, "id"),
                    Cis = cis,
                    Name = stopGroup.Name,
                    Platform = Attr(stop, "platform"),
                    Zone = Attr(stop, "zone"),
                    MainTrafficType = Attr(stop, "mainTrafficType"),
                    Lat = Geo.ParseDouble(Attr(stop, "lat")),
                    Lon = Geo.ParseDouble(Attr(stop, "lon")),
                    AudioFile = stopGroup.AudioFile
                };

                if (IsMetroTraffic(platform.MainTrafficType))
                    stopGroup.HasTransferMetro = true;

                foreach (var line in stop.Elements("line"))
                {
                    var lineType = Attr(line, "type");
                    var lineName = Attr(line, "name").Trim();
                    if (lineType.Equals("train", StringComparison.OrdinalIgnoreCase) && lineName.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                        stopGroup.HasTransferS = true;

                    if (lineType.Equals("metro", StringComparison.OrdinalIgnoreCase) || IsMetroLineName(lineName))
                    {
                        stopGroup.HasTransferMetro = true;
                        if (IsMetroLineName(lineName) && !stopGroup.MetroLines.Contains(lineName, StringComparer.OrdinalIgnoreCase))
                            stopGroup.MetroLines.Add(lineName.ToUpperInvariant());
                    }
                }

                stopGroup.Platforms.Add(platform);
                foreach (var gtfs in gtfsIds)
                {
                    if (!cache.StopsByGtfsId.ContainsKey(gtfs))
                        cache.StopsByGtfsId[gtfs] = platform;
                }
            }

            cache.StopsByCis[cis] = stopGroup;
            if (string.IsNullOrWhiteSpace(stopGroup.AudioFile))
                cache.Report.MissingAudio.Add($"{cis} - {stopGroup.Name} (čekám audio/Zastavky/{cis}.mp3)");
        }

        PropagateTransfersByNode(cache);

        foreach (var group in cache.StopsByCis.Values)
        {
            foreach (var platform in group.Platforms)
            {
                platform.HasTransferS = group.HasTransferS;
                platform.HasTransferMetro = group.HasTransferMetro;
                platform.MetroLines = group.MetroLines.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            }
        }

        cache.Report.StopGroups = cache.StopsByCis.Count;
        cache.Report.Platforms = cache.StopsByGtfsId.Count;
        cache.Report.TransferSStops = cache.StopsByCis.Values.Count(g => g.HasTransferS);
        cache.Report.TransferMetroStops = cache.StopsByCis.Values.Count(g => g.HasTransferMetro);
    }


    private static void LoadPidTransferInfo(AppCache cache)
    {
        var stopsPath = Path.Combine(Paths.Pid, "stops.txt");
        var routeStopsPath = Path.Combine(Paths.Pid, "route_stops.txt");
        if (!File.Exists(stopsPath) || !File.Exists(routeStopsPath))
        {
            cache.Report.Warnings.Add("Přestupy z PID dat nebyly načteny, chybí stops.txt nebo route_stops.txt.");
            return;
        }

        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var stopNode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var stopName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var stopIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string Find(string x)
        {
            if (!parent.TryGetValue(x, out var p))
            {
                parent[x] = x;
                return x;
            }
            if (p == x) return x;
            parent[x] = Find(p);
            return parent[x];
        }

        void Union(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return;
            var ra = Find(a);
            var rb = Find(b);
            if (!ra.Equals(rb, StringComparison.OrdinalIgnoreCase)) parent[rb] = ra;
        }

        foreach (var row in Csv.ReadRows(stopsPath))
        {
            var stopId = row.Get("stop_id");
            if (string.IsNullOrWhiteSpace(stopId)) continue;
            stopIds.Add(stopId);
            _ = Find(stopId);

            var name = row.Get("stop_name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                stopName[stopId] = NormalizeNameKey(name);
                Union(stopId, "NAME:" + NormalizeNameKey(name));
            }

            var node = row.Get("asw_node_id");
            if (!string.IsNullOrWhiteSpace(node))
            {
                stopNode[stopId] = node;
                Union(stopId, "NODE:" + node);
            }

            var parentStation = row.Get("parent_station");
            if (!string.IsNullOrWhiteSpace(parentStation))
                Union(stopId, parentStation);
        }

        void UnionFileStops(string fileName, string fromField, string toField)
        {
            var path = Path.Combine(Paths.Pid, fileName);
            if (!File.Exists(path)) return;
            foreach (var row in Csv.ReadRows(path))
            {
                var from = row.Get(fromField);
                var to = row.Get(toField);
                if (stopIds.Contains(from) && stopIds.Contains(to))
                    Union(from, to);
            }
        }

        UnionFileStops("transfers.txt", "from_stop_id", "to_stop_id");
        UnionFileStops("pathways.txt", "from_stop_id", "to_stop_id");

        var direct = new Dictionary<string, CachedTransferInfo>(StringComparer.OrdinalIgnoreCase);
        var nodeInfo = new Dictionary<string, CachedTransferInfo>(StringComparer.OrdinalIgnoreCase);
        var nameInfo = new Dictionary<string, CachedTransferInfo>(StringComparer.OrdinalIgnoreCase);

        CachedTransferInfo GetInfo(Dictionary<string, CachedTransferInfo> dict, string key)
        {
            if (!dict.TryGetValue(key, out var info))
            {
                info = new CachedTransferInfo();
                dict[key] = info;
            }
            return info;
        }

        static void MergeInfo(CachedTransferInfo target, CachedTransferInfo source)
        {
            target.HasTransferS |= source.HasTransferS;
            target.HasTransferMetro |= source.HasTransferMetro;
            foreach (var metro in source.MetroLines)
                if (!target.MetroLines.Contains(metro, StringComparer.OrdinalIgnoreCase))
                    target.MetroLines.Add(metro);
        }

        foreach (var row in Csv.ReadRows(routeStopsPath))
        {
            var stopId = row.Get("stop_id");
            var routeId = row.Get("route_id");
            if (string.IsNullOrWhiteSpace(stopId) || !cache.RoutesById.TryGetValue(routeId, out var route)) continue;

            var info = GetInfo(direct, stopId);
            var routeType = (route.Type ?? "").Trim();
            var shortName = (route.ShortName ?? "").Trim().ToUpperInvariant();

            if ((routeType == "2" || shortName.StartsWith("S", StringComparison.OrdinalIgnoreCase)) && shortName.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                info.HasTransferS = true;

            if (routeType == "1" || shortName is "A" or "B" or "C")
            {
                info.HasTransferMetro = true;
                if (shortName is "A" or "B" or "C" && !info.MetroLines.Contains(shortName, StringComparer.OrdinalIgnoreCase))
                    info.MetroLines.Add(shortName);
            }

            // Explicitní propagace podle asw_node_id a názvu. Union komponenty by to většinou zvládly,
            // ale PID data mají pár míst, kde se přestup tváří jako cizí ostrov, protože proč by ne.
            if (stopNode.TryGetValue(stopId, out var node))
                MergeInfo(GetInfo(nodeInfo, node), info);
            if (stopName.TryGetValue(stopId, out var nameKey))
                MergeInfo(GetInfo(nameInfo, nameKey), info);
        }

        var componentInfo = new Dictionary<string, CachedTransferInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in direct)
        {
            var root = Find(pair.Key);
            MergeInfo(GetInfo(componentInfo, root), pair.Value);
        }

        foreach (var stopId in stopIds)
        {
            var combined = new CachedTransferInfo();
            var root = Find(stopId);
            if (componentInfo.TryGetValue(root, out var byComponent)) MergeInfo(combined, byComponent);
            if (stopNode.TryGetValue(stopId, out var node) && nodeInfo.TryGetValue(node, out var byNode)) MergeInfo(combined, byNode);
            if (stopName.TryGetValue(stopId, out var nameKey) && nameInfo.TryGetValue(nameKey, out var byName)) MergeInfo(combined, byName);

            if (!combined.HasTransferS && !combined.HasTransferMetro) continue;
            combined.MetroLines = combined.MetroLines.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            cache.TransfersByGtfsId[stopId] = combined;
        }

        foreach (var pair in cache.TransfersByGtfsId)
        {
            if (!cache.StopsByGtfsId.TryGetValue(pair.Key, out var mapped)) continue;
            mapped.HasTransferS |= pair.Value.HasTransferS;
            mapped.HasTransferMetro |= pair.Value.HasTransferMetro;
            foreach (var metro in pair.Value.MetroLines)
                if (!mapped.MetroLines.Contains(metro, StringComparer.OrdinalIgnoreCase))
                    mapped.MetroLines.Add(metro);
        }

        cache.Report.TransferSStops = cache.TransfersByGtfsId.Values.Count(i => i.HasTransferS);
        cache.Report.TransferMetroStops = cache.TransfersByGtfsId.Values.Count(i => i.HasTransferMetro);
        cache.Report.Warnings.Add($"Přestupy dopočteny z PID: stop_id={cache.TransfersByGtfsId.Count}, metro={cache.Report.TransferMetroStops}, S={cache.Report.TransferSStops}.");
    }

    private static string NormalizeNameKey(string value)
    {
        return string.Join(" ", (value ?? "").Trim().ToUpperInvariant()
            .Replace(".", " ")
            .Replace(",", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static void PropagateTransfersByNode(AppCache cache)
    {
        foreach (var nodeGroup in cache.StopsByCis.Values.Where(g => !string.IsNullOrWhiteSpace(g.Node)).GroupBy(g => g.Node, StringComparer.OrdinalIgnoreCase))
        {
            var hasS = nodeGroup.Any(g => g.HasTransferS);
            var hasMetro = nodeGroup.Any(g => g.HasTransferMetro);
            var metroLines = nodeGroup.SelectMany(g => g.MetroLines).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

            foreach (var group in nodeGroup)
            {
                group.HasTransferS |= hasS;
                group.HasTransferMetro |= hasMetro;
                foreach (var metroLine in metroLines)
                {
                    if (!group.MetroLines.Contains(metroLine, StringComparer.OrdinalIgnoreCase))
                        group.MetroLines.Add(metroLine);
                }
            }
        }
    }

    private static bool IsMetroTraffic(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.StartsWith("metro", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMetroLineName(string value)
    {
        value = (value ?? "").Trim().ToUpperInvariant();
        return value is "A" or "B" or "C";
    }

    private static void LoadRoutes(AppCache cache)
    {
        var routesPath = Path.Combine(Paths.Pid, "routes.txt");
        if (!File.Exists(routesPath))
        {
            cache.Report.Warnings.Add("Chybí data/PID/routes.txt");
            return;
        }

        foreach (var row in Csv.ReadRows(routesPath))
        {
            var route = new CachedRoute
            {
                RouteId = row.Get("route_id"),
                ShortName = row.Get("route_short_name"),
                LongName = row.Get("route_long_name"),
                Type = row.Get("route_type")
            };
            if (!string.IsNullOrWhiteSpace(route.RouteId))
                cache.RoutesById[route.RouteId] = route;
        }
    }

    private static void LoadTrips(AppCache cache)
    {
        var tripsPath = Path.Combine(Paths.Pid, "trips.txt");
        if (!File.Exists(tripsPath))
        {
            cache.Report.Warnings.Add("Chybí data/PID/trips.txt");
            return;
        }

        foreach (var row in Csv.ReadRows(tripsPath))
        {
            var routeId = row.Get("route_id");
            cache.RoutesById.TryGetValue(routeId, out var route);
            var trip = new CachedTrip
            {
                TripId = row.Get("trip_id"),
                RouteId = routeId,
                Line = route?.ShortName ?? routeId,
                ServiceId = row.Get("service_id"),
                Headsign = row.Get("trip_headsign"),
                DirectionId = row.Get("direction_id"),
                BlockId = row.Get("block_id"),
                TripShortName = row.Get("trip_short_name")
            };
            if (!string.IsNullOrWhiteSpace(trip.TripId))
                cache.TripsById[trip.TripId] = trip;
        }
    }

    private static void LoadStopTimes(AppCache cache)
    {
        var stopTimesPath = Path.Combine(Paths.Pid, "stop_times.txt");
        if (!File.Exists(stopTimesPath))
        {
            cache.Report.Warnings.Add("Chybí data/PID/stop_times.txt");
            return;
        }

        foreach (var row in Csv.ReadRows(stopTimesPath))
        {
            var tripId = row.Get("trip_id");
            if (!cache.TripsById.TryGetValue(tripId, out var trip)) continue;

            var stopId = row.Get("stop_id");
            cache.StopsByGtfsId.TryGetValue(stopId, out var mapped);
            cache.TransfersByGtfsId.TryGetValue(stopId, out var pidTransfer);
            if (mapped == null && cache.Report.MissingStopMapping.Count < 1000)
            {
                var stopName = row.Get("stop_name");
                cache.Report.MissingStopMapping.Add($"{stopId} {stopName}".Trim());
            }

            var tripStop = new CachedTripStop
            {
                Sequence = int.TryParse(row.Get("stop_sequence"), out var seq) ? seq : 0,
                StopId = stopId,
                GtfsId = mapped?.GtfsId ?? stopId,
                Cis = mapped?.Cis ?? "",
                Name = mapped?.Name ?? row.Get("stop_name"),
                Platform = mapped?.Platform ?? "",
                ArrivalTime = row.Get("arrival_time"),
                DepartureTime = row.Get("departure_time"),
                Zone = mapped?.Zone ?? "",
                Lat = mapped?.Lat ?? 0,
                Lon = mapped?.Lon ?? 0,
                AudioFile = mapped?.AudioFile ?? "",
                ShapeDistTraveled = double.TryParse(row.Get("shape_dist_traveled"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dist) ? dist : null,
                HasTransferS = (mapped?.HasTransferS ?? false) || (pidTransfer?.HasTransferS ?? false),
                HasTransferMetro = (mapped?.HasTransferMetro ?? false) || (pidTransfer?.HasTransferMetro ?? false),
                MetroLines = MergeMetroLines(mapped?.MetroLines, pidTransfer?.MetroLines)
            };
            trip.Stops.Add(tripStop);
            cache.Report.TripStops++;
        }

        foreach (var trip in cache.TripsById.Values)
        {
            trip.Stops = trip.Stops.OrderBy(s => s.Sequence).ToList();
            for (var i = 1; i < trip.Stops.Count; i++)
            {
                var prev = trip.Stops[i - 1];
                var cur = trip.Stops[i];
                if (cur.ShapeDistTraveled != null && prev.ShapeDistTraveled != null && cur.ShapeDistTraveled >= prev.ShapeDistTraveled)
                    cur.DistanceKmFromPrevious = cur.ShapeDistTraveled.Value - prev.ShapeDistTraveled.Value;
                else
                    cur.DistanceKmFromPrevious = Geo.DistanceKm(prev.Lat, prev.Lon, cur.Lat, cur.Lon);
            }
        }

    }

    private static void BuildTurnuses(AppCache cache)
    {
        var groups = cache.TripsById.Values
            .Where(t => !string.IsNullOrWhiteSpace(t.BlockId))
            .GroupBy(t => t.BlockId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var turnus = new CachedTurnus
            {
                Id = group.Key,
                Name = "Oběh / turnus " + group.Key,
                Source = "GTFS block_id"
            };

            foreach (var trip in group.OrderBy(t => t.Stops.FirstOrDefault()?.DepartureTime ?? "99:99:99"))
            {
                if (trip.Stops.Count == 0) continue;
                var first = trip.Stops.First();
                var last = trip.Stops.Last();
                turnus.Items.Add(new CachedTurnusItem
                {
                    TripId = trip.TripId,
                    Line = trip.Line,
                    StartTime = first.DepartureTime,
                    From = first.Name,
                    To = last.Name
                });
            }

            if (turnus.Items.Count > 0)
                cache.Turnuses.Add(turnus);
        }
    }


    private static List<string> MergeMetroLines(IEnumerable<string>? a, IEnumerable<string>? b)
    {
        return (a ?? Enumerable.Empty<string>())
            .Concat(b ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => x is "A" or "B" or "C")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static string GuessAudioFile(string cis)
    {
        var candidates = new[]
        {
            Path.Combine(Paths.Audio, "Zastavky", cis + ".mp3"),
            Path.Combine(Paths.Audio, "Zastávky", cis + ".mp3"),
            Path.Combine(Paths.Audio, cis + ".mp3")
        };
        var found = candidates.FirstOrDefault(File.Exists);
        return found == null ? "" : Path.GetRelativePath(Paths.Root, found).Replace('\\', '/');
    }

    private static string Attr(XElement el, string name) => el.Attribute(name)?.Value ?? "";
}
