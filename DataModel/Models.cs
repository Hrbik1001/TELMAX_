using System;
using System.Collections.Generic;

namespace PIDMobileSpeaker;

public sealed class UsersFile
{
    public List<AppUser> Users { get; set; } = new();
}

public sealed class AppUser
{
    public string Code { get; set; } = "";
    public string Password { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class PhrasesFile
{
    public Dictionary<string, string> Phrases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DodFile
{
    public List<DodAnnouncement> Announcements { get; set; } = new();
}

public sealed class DodAnnouncement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

public sealed class AppCache
{
    public Dictionary<string, CachedStop> StopsByGtfsId { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CachedTransferInfo> TransfersByGtfsId { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CachedStopGroup> StopsByCis { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CachedRoute> RoutesById { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CachedTrip> TripsById { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CachedTurnus> Turnuses { get; set; } = new();
    public ImportReport Report { get; set; } = new();
}


public sealed class CachedTransferInfo
{
    public bool HasTransferS { get; set; }
    public bool HasTransferMetro { get; set; }
    public List<string> MetroLines { get; set; } = new();
}

public sealed class CachedStopGroup
{
    public string Cis { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Node { get; set; } = "";
    public string MainTrafficType { get; set; } = "";
    public double AvgLat { get; set; }
    public double AvgLon { get; set; }
    public string AudioFile { get; set; } = "";
    public bool HasTransferS { get; set; }
    public bool HasTransferMetro { get; set; }
    public List<string> MetroLines { get; set; } = new();
    public List<CachedStop> Platforms { get; set; } = new();
}

public sealed class CachedStop
{
    public string GtfsId { get; set; } = "";
    public string StopId { get; set; } = "";
    public string Cis { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Zone { get; set; } = "";
    public string MainTrafficType { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string AudioFile { get; set; } = "";
    public bool HasTransferS { get; set; }
    public bool HasTransferMetro { get; set; }
    public List<string> MetroLines { get; set; } = new();
}

public sealed class CachedRoute
{
    public string RouteId { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string LongName { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class CachedTrip
{
    public string TripId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string Line { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string Headsign { get; set; } = "";
    public string DirectionId { get; set; } = "";
    public string BlockId { get; set; } = "";
    public string TripShortName { get; set; } = "";
    public List<CachedTripStop> Stops { get; set; } = new();
}

public sealed class CachedTripStop
{
    public int Sequence { get; set; }
    public string StopId { get; set; } = "";
    public string GtfsId { get; set; } = "";
    public string Cis { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string ArrivalTime { get; set; } = "";
    public string DepartureTime { get; set; } = "";
    public string Zone { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double DistanceKmFromPrevious { get; set; }
    public double? ShapeDistTraveled { get; set; }
    public string AudioFile { get; set; } = "";
    public bool HasTransferS { get; set; }
    public bool HasTransferMetro { get; set; }
    public List<string> MetroLines { get; set; } = new();
}

public sealed class CachedTurnus
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public List<CachedTurnusItem> Items { get; set; } = new();
}

public sealed class CachedTurnusItem
{
    public string TripId { get; set; } = "";
    public string Line { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class ImportReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public int StopGroups { get; set; }
    public int Platforms { get; set; }
    public int Routes { get; set; }
    public int Trips { get; set; }
    public int TripStops { get; set; }
    public int Turnuses { get; set; }
    public int TransferSStops { get; set; }
    public int TransferMetroStops { get; set; }
    public List<string> MissingAudio { get; set; } = new();
    public List<string> MissingStopMapping { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
