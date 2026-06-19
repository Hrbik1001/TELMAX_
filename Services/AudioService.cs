using System.Collections.Concurrent;

#if ANDROID
using Android.Media;
#endif

namespace PIDMobileSpeaker.Services;

public sealed class AudioService
{
    private readonly SemaphoreSlim _playLock = new(1, 1);

    public async Task PlaySequenceAsync(IEnumerable<string> relativeFiles, CancellationToken token = default)
    {
        await _playLock.WaitAsync(token);

        try
        {
            foreach (var rel in relativeFiles.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                token.ThrowIfCancellationRequested();

                var path = ResolveAudioPath(rel);
                if (path == null)
                {
                    Logger.Error("Chybí audio: " + rel);
                    continue;
                }

                Logger.Info("Audio: " + path);
                await PlayFileAsync(path, token);
            }
        }
        finally
        {
            _playLock.Release();
        }
    }

    public string? ResolveAudioPath(string relative)
    {
        var clean = relative.Replace("\\", "/").Trim();

        var candidates = new[]
        {
            Path.Combine(Paths.Root, clean),
            Path.Combine(Paths.Root, clean.Replace("audio/", "Audio/", StringComparison.OrdinalIgnoreCase)),
            Path.Combine(Paths.Audio, clean),
            Path.Combine(Paths.Audio, Path.GetFileName(clean)),
            Path.Combine(Paths.Audio, "System", Path.GetFileName(clean)),
            Path.Combine(Paths.Audio, "Zastavky", Path.GetFileName(clean)),
            Path.Combine(Paths.Audio, "Zastávky", Path.GetFileName(clean))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            var fileName = Path.GetFileName(clean);

            if (!string.IsNullOrWhiteSpace(fileName) && Directory.Exists(Paths.Audio))
            {
                return Directory
                    .EnumerateFiles(Paths.Audio, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
        }
        catch
        {
            // Audio hledání nesmí shodit celou aplikaci. To by bylo až moc androidí.
        }

        return null;
    }

#if ANDROID
    private static Task PlayFileAsync(string path, CancellationToken token)
    {
        var tcs = new TaskCompletionSource();

        var player = new MediaPlayer();
        player.SetDataSource(path);
        player.Prepare();

        token.Register(() =>
        {
            try
            {
                player.Stop();
            }
            catch
            {
                // ignored
            }

            try
            {
                player.Release();
            }
            catch
            {
                // ignored
            }

            tcs.TrySetCanceled(token);
        });

        player.Completion += (_, _) =>
        {
            try
            {
                player.Release();
            }
            catch
            {
                // ignored
            }

            tcs.TrySetResult();
        };

        player.Error += (_, args) =>
        {
            try
            {
                player.Release();
            }
            catch
            {
                // ignored
            }

            tcs.TrySetException(new InvalidOperationException($"MediaPlayer error {args.What}/{args.Extra}"));
        };

        player.Start();
        return tcs.Task;
    }
#else
    private static Task PlayFileAsync(string path, CancellationToken token)
    {
        Logger.Info("Audio stub: " + path);
        return Task.Delay(250, token);
    }
#endif
}
