using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The wifi details the booth leaves in the shared sessions folder for the
/// claiming station.
/// </summary>
public sealed class SharedSettingsTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"pb-shared-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void What_the_booth_writes_the_station_reads_back()
    {
        var written = new SharedSettings("Booth", "smile123", DateTimeOffset.UnixEpoch);

        SharedSettings.Write(_folder, written);

        Assert.Equal(written, SharedSettings.Read(_folder));
    }

    [Fact]
    public void Nothing_published_reads_as_nothing()
    {
        Assert.Null(SharedSettings.Read(_folder));
    }

    /// <summary>A half-copied or hand-edited file must not take the station down.</summary>
    [Fact]
    public void A_damaged_file_reads_as_nothing()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, SharedSettings.FileName), "{ \"guestWifiSsid\": ");

        Assert.Null(SharedSettings.Read(_folder));
    }

    /// <summary>
    /// It sits beside the guests' folders, and the station lists those folders.
    /// No temporary file may be left there for it to trip over.
    /// </summary>
    [Fact]
    public void Writing_leaves_only_the_one_file()
    {
        SharedSettings.Write(_folder, new SharedSettings("Booth", null, DateTimeOffset.UnixEpoch));
        SharedSettings.Write(_folder, new SharedSettings("Booth 2", null, DateTimeOffset.UnixEpoch));

        Assert.Equal([SharedSettings.FileName], Directory.GetFiles(_folder).Select(Path.GetFileName));
        Assert.Equal("Booth 2", SharedSettings.Read(_folder)?.GuestWifiSsid);
    }
}
