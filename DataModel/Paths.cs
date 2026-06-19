using System.IO;
using Microsoft.Maui.Storage;

namespace PIDMobileSpeaker;

public static class Paths
{
    public static string Root => FileSystem.AppDataDirectory;
    public static string Data => Path.Combine(Root, "data");
    public static string Cache => Path.Combine(Root, "cache");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Audio => Directory.Exists(Path.Combine(Root, "Audio")) ? Path.Combine(Root, "Audio") : Path.Combine(Root, "audio");
    public static string Pid => Path.Combine(Data, "PID");
    public static string CacheFile => Path.Combine(Cache, "app_cache.json");
    public static string UsersFile => Path.Combine(Data, "users.json");
    public static string PhrasesFile => Path.Combine(Data, "phrases.json");
    public static string DodFile => Path.Combine(Data, "DOD.json");
    public static string StopsByNameFile => Path.Combine(Data, "StopsByName.xml");

    public static void EnsureFolders()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Audio);
    }
}
