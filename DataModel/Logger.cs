using System;
using System.IO;

namespace PIDMobileSpeaker;

public static class Logger
{
    public static event Action<string>? LineWritten;

    public static void Info(string text) => Write("actions.log", "INFO", text);
    public static void Error(string text) => Write("errors.log", "ERROR", text);

    private static void Write(string file, string level, string text)
    {
        try
        {
            Paths.EnsureFolders();
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {text}";
            File.AppendAllText(Path.Combine(Paths.Logs, file), line + Environment.NewLine);
            LineWritten?.Invoke(line);
        }
        catch
        {
            // Mobil nemá potřebu padat jen proto, že se mu nelíbí log. To by byla až moc úřednická funkce.
        }
    }
}
