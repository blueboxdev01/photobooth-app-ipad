namespace Photobooth.Server;

/// <summary>One finished guest, as the claiming station shows them.</summary>
/// <param name="Token">The session's own unguessable id; what the photos QR carries.</param>
/// <param name="Folder">The session's folder name, to find its strip.</param>
/// <param name="StripUrl">Where the strip can be fetched, so a guest recognises theirs.</param>
public sealed record ClaimEntry(
    string Token, string Folder, string StripUrl, DateTimeOffset FinishedUtc);

/// <summary>
/// The guests who have finished and may still be walking to the claiming station.
///
/// <para>
/// Kept apart from <see cref="Photobooth.Core.SessionEngine"/> on purpose. The
/// engine holds one session and clears it the moment the next guest is armed --
/// right for the booth screen, which must never offer the last guest's photos to
/// the next one, and wrong for a claiming station, where guest one is still
/// scanning while guest two is in front of the camera.
/// </para>
///
/// <para>
/// A handful of guests for a few minutes, not an archive. The photos link is the
/// whole of a session's security, and every entry here is one a device on the
/// guests' wifi can ask for, so the list is kept as short as the walk is long.
/// </para>
/// </summary>
public sealed class ClaimBoard(TimeProvider time)
{
    /// <summary>
    /// Guests shown at once. More than one so two people finishing close together
    /// do not push each other off the screen, and not many more than that.
    /// </summary>
    public const int MaxEntries = 3;

    /// <summary>
    /// How long a guest stays on the board. Walking to the station takes seconds;
    /// this is for the one who stops to chat first.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly Lock _sync = new();
    private readonly List<ClaimEntry> _entries = [];

    /// <summary>Add a finished guest at the front. The same session twice replaces itself.</summary>
    public void Add(ClaimEntry entry)
    {
        lock (_sync)
        {
            _entries.RemoveAll(e => e.Token == entry.Token);
            _entries.Insert(0, entry);

            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }
        }
    }

    /// <summary>The guests still on the board, newest first.</summary>
    public IReadOnlyList<ClaimEntry> Recent()
    {
        lock (_sync)
        {
            var cutoff = time.GetUtcNow() - Lifetime;
            _entries.RemoveAll(e => e.FinishedUtc < cutoff);
            return [.. _entries];
        }
    }
}
