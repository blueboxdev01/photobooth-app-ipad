namespace Photobooth.Server;

/// <summary>
/// The guest the claiming station is showing on its guest-facing monitor.
///
/// <para>
/// The station has two screens: the operator's, with every guest of the day on
/// it, and a monitor turned towards the guest that shows one strip and its QR.
/// The operator picks; this remembers the pick so the monitor's page can show it.
/// Both pages run on the station itself, so this is operator port only -- the
/// guest's token never goes near the wifi except inside their own QR.
/// </para>
///
/// <para>
/// Cleared on its own after a few minutes, so a guest who wandered off does not
/// leave their code up for whoever comes next.
/// </para>
/// </summary>
public sealed class PickupScreen(TimeProvider time)
{
    /// <summary>Long enough to scan and download; short enough not to linger.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    private readonly Lock _sync = new();
    private PickupGuest? _guest;
    private DateTimeOffset _shownAt;

    public void Show(PickupGuest guest)
    {
        lock (_sync)
        {
            _guest = guest;
            _shownAt = time.GetUtcNow();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _guest = null;
        }
    }

    /// <summary>The guest on the monitor, or null once cleared or expired.</summary>
    public PickupGuest? Current()
    {
        lock (_sync)
        {
            if (_guest is not null && time.GetUtcNow() - _shownAt >= Lifetime)
            {
                _guest = null;
            }

            return _guest;
        }
    }
}
