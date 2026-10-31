using Microsoft.Extensions.Time.Testing;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The guest the claiming station's monitor is showing. It holds one guest's
/// photos link up for anyone standing there, so it must let go of it.
/// </summary>
public sealed class PickupScreenTests
{
    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    private readonly PickupScreen _screen;

    public PickupScreenTests() => _screen = new PickupScreen(_time);

    private static PickupGuest Guest(int number) =>
        new(number, $"token-{number}", $"folder-{number}", $"/api/sessions/folder-{number}/strip.jpg",
            4, true, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Nobody_is_shown_until_the_operator_picks_someone()
    {
        Assert.Null(_screen.Current());
    }

    [Fact]
    public void Picking_a_guest_replaces_whoever_was_shown()
    {
        _screen.Show(Guest(1));
        _screen.Show(Guest(2));

        Assert.Equal(2, _screen.Current()?.Number);
    }

    [Fact]
    public void Clearing_takes_the_code_off_the_monitor()
    {
        _screen.Show(Guest(1));
        _screen.Clear();

        Assert.Null(_screen.Current());
    }

    [Fact]
    public void A_guest_left_on_screen_goes_after_a_few_minutes()
    {
        _screen.Show(Guest(1));

        _time.Advance(PickupScreen.Lifetime - TimeSpan.FromSeconds(1));
        Assert.NotNull(_screen.Current());

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(_screen.Current());
    }
}
