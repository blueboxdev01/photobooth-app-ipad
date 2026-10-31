using Photobooth.Delivery;

namespace Photobooth.Server;

/// <summary>One guest on the claiming station's grid.</summary>
public sealed record PickupGuest(
    int? Number,
    string Token,
    string Folder,
    string StripUrl,
    int PhotoCount,
    bool HasVideo,
    DateTimeOffset CreatedUtc);

/// <summary>What the claiming station shows for one event day.</summary>
public sealed record PickupDay(
    string? Day,
    IReadOnlyList<string> Days,
    IReadOnlyList<PickupGuest> Guests);

/// <summary>
/// The claiming station's view of the sessions folder: every guest for one event
/// day, labelled with the number they were given.
///
/// <para>
/// The sessions folder is usually a network share filled by the booth computer,
/// where each directory listing and each session.json is a round trip. The
/// station's page polls every few seconds and the guest page resolves a token on
/// every request, so the result is kept for a moment rather than rebuilt for each
/// of them. A new guest appears within that window, which is well inside the
/// time it takes the guest to walk over.
/// </para>
///
/// <para>
/// Operator port only. The list carries every guest's token, and a token is the
/// whole of a guest's security, so it must never be reachable from the guests'
/// wifi. See <see cref="GuestEndpoint"/> for what that port allows.
/// </para>
/// </summary>
public sealed class PickupBoard(SessionArchive archive, TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);

    private readonly Lock _sync = new();
    private DateTimeOffset _loadedAt;
    private IReadOnlyList<SessionRecord> _records = [];

    /// <param name="day">
    /// <c>yyyy-MM-dd</c>, or null for today when there are sessions today and the
    /// most recent day otherwise.
    /// </param>
    public PickupDay For(string? day)
    {
        var records = Records();
        var days = records
            .Select(SessionArchive.DayOf)
            .Where(d => d.Length > 0)
            .Distinct()
            .OrderByDescending(d => d, StringComparer.Ordinal)
            .ToList();

        return Build(records, days, Choose(day, days, SessionArchive.DayOf(time.GetUtcNow())));
    }

    /// <summary>One guest by their session folder, whatever day they came.</summary>
    public PickupGuest? Find(string folder)
    {
        var record = Records().FirstOrDefault(
            r => string.Equals(r.FolderName, folder, StringComparison.Ordinal));
        return record is null ? null : Build([record], [], SessionArchive.DayOf(record)).Guests[0];
    }

    /// <summary>
    /// Which day to show. A requested day is honoured even when it has no
    /// sessions, so a typo shows an empty grid instead of a different day.
    /// </summary>
    internal static string? Choose(string? requested, IReadOnlyList<string> days, string today)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested;
        }

        if (days.Contains(today))
        {
            return today;
        }

        return days.Count > 0 ? days[0] : null;
    }

    /// <summary>
    /// Guests in number order. Sessions with no number (made before numbers, or
    /// with hand-over by number off) follow, oldest first, so they are still
    /// findable.
    /// </summary>
    internal static PickupDay Build(
        IReadOnlyList<SessionRecord> records, IReadOnlyList<string> days, string? day)
    {
        var guests = records
            .Where(r => day is not null && SessionArchive.DayOf(r) == day)
            .OrderBy(r => r.Number is null)
            .ThenBy(r => r.Number)
            .ThenBy(r => r.CreatedUtc)
            .Select(r => new PickupGuest(
                r.Number,
                r.Token,
                r.FolderName,
                $"/api/sessions/{r.FolderName}/{r.Strip}",
                r.Photos.Count,
                r.Animation is not null,
                r.CreatedUtc))
            .ToList();

        return new PickupDay(day, days, guests);
    }

    private IReadOnlyList<SessionRecord> Records()
    {
        lock (_sync)
        {
            var now = time.GetUtcNow();
            if (now - _loadedAt >= Lifetime || _loadedAt == default)
            {
                _records = archive.All();
                _loadedAt = now;
            }

            return _records;
        }
    }
}
