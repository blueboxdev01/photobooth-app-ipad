using System.Text.Json;
using Photobooth.Delivery;

namespace Photobooth.Server;

/// <summary>
/// The part of the booth's settings the claiming station needs too, as the booth
/// writes it into the sessions folder they share.
///
/// <para>
/// The booth computer is where the event is set up. Typing the same wifi name
/// and password into a second machine is a second chance to get one letter
/// wrong, and a join code that scans and does nothing. So the booth publishes
/// them beside the sessions, and the station reads them from there.
/// </para>
///
/// <para>
/// A file next to the session folders rather than a request to the booth: the
/// station already reads that folder, and needs nothing else to be reachable.
/// <see cref="SessionArchive.All"/> lists folders only, so it never mistakes this
/// for a guest.
/// </para>
/// </summary>
public sealed record SharedSettings(
    string? GuestWifiSsid,
    string? GuestWifiPassword,
    DateTimeOffset WrittenUtc)
{
    public const string FileName = "booth-settings.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Written to a temporary name and moved into place, so a station reading
    /// over the network never sees half a file.
    /// </summary>
    public static void Write(string folder, SharedSettings settings)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>What the booth last published, or null when there is nothing readable.</summary>
    public static SharedSettings? Read(string folder)
    {
        try
        {
            var path = Path.Combine(folder, FileName);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SharedSettings>(File.ReadAllText(path), Json)
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// The guests' wifi, wherever this machine gets it from: its own settings on the
/// booth, and the booth's published ones on a claiming station.
///
/// <para>
/// On the booth computer this also does the publishing: once at start, and again
/// whenever the settings are saved, so a corrected password reaches the station
/// without anyone touching it.
/// </para>
/// </summary>
public sealed class GuestWifi : IHostedService
{
    /// <summary>The share is read at most this often; the station's pages poll every second.</summary>
    private static readonly TimeSpan ReadLifetime = TimeSpan.FromSeconds(5);

    private readonly SettingsStore _store;
    private readonly SessionArchive _archive;
    private readonly TimeProvider _time;
    private readonly ILogger<GuestWifi> _logger;
    private readonly Lock _sync = new();
    private SharedSettings? _shared;
    private DateTimeOffset _readAt;

    public GuestWifi(
        SettingsStore store, SessionArchive archive, TimeProvider time, ILogger<GuestWifi> logger)
    {
        _store = store;
        _archive = archive;
        _time = time;
        _logger = logger;
    }

    private bool IsStation => _store.Current.ClaimingStationOnly == true;

    /// <summary>
    /// What the booth published, when this is a claiming station and the booth
    /// has published anything. Null on the booth itself.
    /// </summary>
    public SharedSettings? FromBooth()
    {
        if (!IsStation)
        {
            return null;
        }

        lock (_sync)
        {
            var now = _time.GetUtcNow();
            if (_readAt == default || now - _readAt >= ReadLifetime)
            {
                _shared = SharedSettings.Read(_archive.Root);
                _readAt = now;
            }

            return _shared;
        }
    }

    /// <summary>The booth's name for it on a station, falling back to this machine's own.</summary>
    public string? Ssid =>
        FromBooth()?.GuestWifiSsid is { Length: > 0 } shared ? shared : _store.Current.GuestWifiSsid;

    /// <summary>Taken together with <see cref="Ssid"/>, so a join code never mixes two networks.</summary>
    public string? Password =>
        FromBooth() is { GuestWifiSsid.Length: > 0 } shared
            ? shared.GuestWifiPassword
            : _store.Current.GuestWifiPassword;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsStation)
        {
            Publish(_store.Current);
        }

        _store.Saved += OnSaved;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _store.Saved -= OnSaved;
        return Task.CompletedTask;
    }

    private void OnSaved(BoothSettings settings)
    {
        if (settings.ClaimingStationOnly == true)
        {
            // Read afresh next time, in case the output folder just moved.
            lock (_sync)
            {
                _readAt = default;
            }

            return;
        }

        Publish(settings);
    }

    private void Publish(BoothSettings settings)
    {
        try
        {
            SharedSettings.Write(_archive.Root, new SharedSettings(
                settings.GuestWifiSsid, settings.GuestWifiPassword, _time.GetUtcNow()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Costs the station its copy of the wifi details, not the booth a
            // guest. Said in the log rather than stopping anything.
            _logger.LogWarning(ex,
                "Could not publish the wifi details to {Folder} for the claiming station.",
                _archive.Root);
        }
    }
}
