namespace Photobooth.Server;

/// <summary>What a screen is told about the preview, so it can fetch it.</summary>
/// <param name="Key">
/// The shots the preview was drawn from, in strip order. A screen shows the
/// preview only when this matches the shots it is showing -- after a reorder or
/// a retake the old picture is wrong, and the key is how it knows.
/// </param>
/// <param name="Url">Where to fetch it; changes with every new preview.</param>
public sealed record StripPreviewInfo(string Key, string Url);

/// <summary>
/// The latest preview of the strip, held in memory.
///
/// <para>
/// One at a time, never written to disk: it exists for the few seconds a guest
/// and the operator spend deciding, and the real strip is composed from the real
/// photos when they press Accept.
/// </para>
/// </summary>
public sealed class StripPreviewStore
{
    private readonly Lock _sync = new();
    private byte[]? _bytes;
    private string? _key;
    private long _version;

    /// <summary>
    /// The key the most recent request was for, whether or not it has finished.
    /// Lets a slow render that was overtaken by a newer one throw itself away.
    /// </summary>
    private string? _wanted;

    /// <summary>
    /// Note that a preview for <paramref name="key"/> is about to be drawn.
    /// Returns false when that is already stored or already in progress.
    /// </summary>
    public bool Want(string key)
    {
        lock (_sync)
        {
            if (_wanted == key)
            {
                return false;
            }

            _wanted = key;
            return true;
        }
    }

    /// <summary>
    /// Store a finished preview, unless a newer request has overtaken it.
    /// Returns what to announce, or null when it was discarded.
    /// </summary>
    public StripPreviewInfo? Complete(string key, byte[] bytes)
    {
        lock (_sync)
        {
            if (_wanted != key)
            {
                return null;
            }

            _bytes = bytes;
            _key = key;
            _version++;
            return new StripPreviewInfo(key, $"/api/session/preview.jpg?v={_version}");
        }
    }

    /// <summary>Forget everything, for the next guest.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _bytes = null;
            _key = null;
            _wanted = null;
        }
    }

    /// <summary>The preview a screen should be offered right now, or null.</summary>
    public StripPreviewInfo? Current()
    {
        lock (_sync)
        {
            return _key is null ? null : new StripPreviewInfo(_key, $"/api/session/preview.jpg?v={_version}");
        }
    }

    public byte[]? Bytes()
    {
        lock (_sync)
        {
            return _bytes;
        }
    }

    /// <summary>
    /// A failed render must not leave <see cref="Want"/> refusing to try again
    /// for the same shots.
    /// </summary>
    public void Failed(string key)
    {
        lock (_sync)
        {
            if (_wanted == key)
            {
                _wanted = null;
            }
        }
    }
}
