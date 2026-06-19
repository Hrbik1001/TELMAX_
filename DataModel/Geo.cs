using System;
using System.Globalization;

namespace PIDMobileSpeaker;

public static class Geo
{
    public static double ParseDouble(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        if (lat1 == 0 || lat2 == 0) return 0;
        const double radius = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(radius * c, 3);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
