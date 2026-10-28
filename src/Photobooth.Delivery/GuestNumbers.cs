namespace Photobooth.Delivery;

/// <summary>
/// The short number a guest is given at Accept and says aloud at the claiming
/// station: 1, 2, 3 ... restarting each event day.
///
/// <para>
/// There is no counter file. The next number is the highest one already on disk
/// for that day plus one, worked out from the archive the first time a day is
/// asked for. A restart therefore carries on where it left off, a session
/// folder deleted by hand frees its number, and there is no second record that
/// could disagree with the archive about what has been handed out.
/// </para>
///
/// <para>
/// The day is the booth's local calendar day, taken from the session folder
/// name (see <see cref="SessionArchive.DayOf(SessionRecord)"/>). Numbers are
/// only unique within a day, which is why the claiming station asks for the
/// day as well as the number.
/// </para>
/// </summary>
public sealed class GuestNumbers(SessionArchive archive)
{
    private readonly Lock _sync = new();
    private string? _day;
    private int _last;

    /// <summary>
    /// The next number for a session that started at <paramref name="startedUtc"/>.
    /// </summary>
    public int Next(DateTimeOffset startedUtc)
    {
        var day = SessionArchive.DayOf(startedUtc);

        lock (_sync)
        {
            if (_day != day)
            {
                _day = day;
                _last = Highest(day);
            }

            return ++_last;
        }
    }

    /// <summary>
    /// Forget the in-memory count, so the next number is worked out from the
    /// archive again. For when sessions have been deleted by hand.
    /// </summary>
    public void Rescan()
    {
        lock (_sync)
        {
            _day = null;
        }
    }

    private int Highest(string day) =>
        archive.All()
            .Where(r => r.Number is not null && SessionArchive.DayOf(r) == day)
            .Select(r => r.Number!.Value)
            .DefaultIfEmpty(0)
            .Max();
}
