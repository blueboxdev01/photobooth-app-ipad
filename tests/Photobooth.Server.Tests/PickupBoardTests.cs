using Photobooth.Delivery;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>What the claiming station shows for a day, and which day.</summary>
public sealed class PickupBoardTests
{
    private static SessionRecord Record(
        string folder, string token, int? number, int minutes = 0, string? animation = "strip.mp4") =>
        new(token, folder, new DateTimeOffset(2026, 10, 28, 12, minutes, 0, TimeSpan.Zero),
            "classic", 4, "strip.jpg",
            ["photo-1.jpg", "photo-2.jpg", "photo-3.jpg", "photo-4.jpg"],
            ["a", "b", "c", "d"], Animation: animation, Number: number);

    [Fact]
    public void Guests_are_listed_in_number_order_whatever_order_they_were_read_in()
    {
        var records = new[]
        {
            Record("2026-10-28_1203_c", "c", 3, 3),
            Record("2026-10-28_1201_a", "a", 1, 1),
            Record("2026-10-28_1202_b", "b", 2, 2),
        };

        var day = PickupBoard.Build(records, ["2026-10-28"], "2026-10-28");

        Assert.Equal([1, 2, 3], day.Guests.Select(g => g.Number!.Value));
    }

    /// <summary>Numbers restart each day, so another day's number 1 is a different guest.</summary>
    [Fact]
    public void Only_the_chosen_days_guests_are_listed()
    {
        var records = new[]
        {
            Record("2026-10-27_1801_x", "x", 1),
            Record("2026-10-28_1201_a", "a", 1),
        };

        var day = PickupBoard.Build(records, ["2026-10-28", "2026-10-27"], "2026-10-28");

        Assert.Equal(["a"], day.Guests.Select(g => g.Token));
    }

    [Fact]
    public void Guests_with_no_number_come_after_the_numbered_ones_oldest_first()
    {
        var records = new[]
        {
            Record("2026-10-28_1205_n2", "n2", null, 5),
            Record("2026-10-28_1201_a", "a", 1, 1),
            Record("2026-10-28_1204_n1", "n1", null, 4),
        };

        var day = PickupBoard.Build(records, ["2026-10-28"], "2026-10-28");

        Assert.Equal(["a", "n1", "n2"], day.Guests.Select(g => g.Token));
    }

    [Fact]
    public void A_guest_carries_what_the_tile_and_the_dialog_need()
    {
        var day = PickupBoard.Build(
            [Record("2026-10-28_1201_a", "tok", 12)], ["2026-10-28"], "2026-10-28");

        var guest = Assert.Single(day.Guests);
        Assert.Equal(12, guest.Number);
        Assert.Equal("tok", guest.Token);
        Assert.Equal("/api/sessions/2026-10-28_1201_a/strip.jpg", guest.StripUrl);
        Assert.Equal(4, guest.PhotoCount);
        Assert.True(guest.HasVideo);
    }

    [Fact]
    public void A_session_without_a_video_says_so()
    {
        var day = PickupBoard.Build(
            [Record("2026-10-28_1201_a", "a", 1, animation: null)], ["2026-10-28"], "2026-10-28");

        Assert.False(day.Guests[0].HasVideo);
    }

    [Fact]
    public void With_no_day_asked_for_today_is_shown_when_it_has_guests()
    {
        var days = new[] { "2026-10-28", "2026-10-27" };

        Assert.Equal("2026-10-28", PickupBoard.Choose(null, days, today: "2026-10-28"));
    }

    [Fact]
    public void With_no_guests_today_the_latest_day_there_is_shown()
    {
        var days = new[] { "2026-10-27", "2026-10-20" };

        Assert.Equal("2026-10-27", PickupBoard.Choose(null, days, today: "2026-10-28"));
    }

    [Fact]
    public void A_day_that_was_asked_for_is_honoured_even_when_it_is_empty()
    {
        Assert.Equal("2026-01-01", PickupBoard.Choose("2026-01-01", ["2026-10-28"], today: "2026-10-28"));
    }

    [Fact]
    public void An_empty_archive_has_no_day_and_no_guests()
    {
        var day = PickupBoard.Build([], [], PickupBoard.Choose(null, [], today: "2026-10-28"));

        Assert.Null(day.Day);
        Assert.Empty(day.Guests);
    }
}
