using System.Collections.Concurrent;
using Photobooth.Imaging;

namespace Photobooth.Server;

/// <summary>
/// Reduced copies of the camera's photos, made when a screen asks for one.
///
/// The guest display shows each shot a few hundred pixels wide but was being
/// sent the 8 MB original, over the same router wifi the guests are on. A
/// preview is a few hundred kilobytes and made once: the screen fetches each shot
/// on arrival and again if it redraws.
/// </summary>
internal static class PhotoPreviews
{
    /// <summary>
    /// A session is a handful of shots, so this holds several guests' worth.
    /// Cleared outright when full -- recomputing one is a fraction of a second,
    /// and a smarter eviction policy would be more code than the problem is worth.
    /// </summary>
    private const int MaxEntries = 32;

    private static readonly ConcurrentDictionary<(string Path, long Length, long Ticks, int Edge), byte[]> Cache = new();

    /// <summary>
    /// <paramref name="path"/> as a JPEG of at most <paramref name="edge"/> pixels
    /// on its longest side, or null when it could not be decoded (the caller then
    /// serves the original). Keyed on the file's size and write time as well as
    /// its name, because EOS Utility reuses names across a power cycle.
    /// </summary>
    public static byte[]? Get(string path, int edge)
    {
        var info = new FileInfo(path);
        var key = (path, info.Length, info.LastWriteTimeUtc.Ticks, edge);

        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bytes = Thumbnails.Render(path, edge, quality: 85);
        if (bytes is null)
        {
            return null;
        }

        if (Cache.Count >= MaxEntries)
        {
            Cache.Clear();
        }

        Cache[key] = bytes;
        return bytes;
    }
}
