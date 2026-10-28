using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Photobooth.Core;
using Photobooth.Delivery;

namespace Photobooth.Delivery.Tests;

/// <summary>
/// The number a guest says at the claiming station. Two guests with the same
/// number on the same day is two people handed each other's photos, so the
/// rules here are pinned rather than assumed.
/// </summary>
public sealed class GuestNumbersTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"pb-numbers-{Guid.NewGuid():N}");

    private readonly SessionArchive _archive;
    private readonly GuestNumbers _numbers;

    // Noon, so a local-time day boundary can never fall inside a test.
    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 28, 12, 0, 0)));

    public GuestNumbersTests()
    {
        _archive = new SessionArchive(
            Options.Create(new ArchiveOptions { Folder = _root }),
            NullLogger<SessionArchive>.Instance);
        _numbers = new GuestNumbers(_archive);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private SessionRecord Save(string token, DateTimeOffset at, int? number)
    {
        Directory.CreateDirectory(_root);
        var photo = Path.Combine(_root, $"src-{token}.jpg");
        File.WriteAllBytes(photo, [0xFF, 0xD8, 0xFF, 0xD9]);

        var template = new StripTemplate(
            "fake", new TemplateCanvas(600, 1800), [new TemplateSlot(0, 0, 1, 0.3)]);

        return _archive.Save(
            token, template,
            [new CapturedPhoto(photo, "IMG_0001.JPG", 4, at)],
            photo, at, number: number);
    }

    [Fact]
    public void The_first_guest_of_the_day_is_number_one_and_then_it_counts_up()
    {
        Assert.Equal(1, _numbers.Next(Noon));
        Assert.Equal(2, _numbers.Next(Noon.AddMinutes(3)));
        Assert.Equal(3, _numbers.Next(Noon.AddMinutes(6)));
    }

    [Fact]
    public void A_new_day_starts_again_from_one()
    {
        Assert.Equal(1, _numbers.Next(Noon));
        Assert.Equal(2, _numbers.Next(Noon.AddMinutes(1)));

        Assert.Equal(1, _numbers.Next(Noon.AddDays(1)));
    }

    /// <summary>
    /// A restart in the middle of an event must carry on, not hand out number
    /// one again to a second guest.
    /// </summary>
    [Fact]
    public void A_restart_carries_on_from_what_is_already_on_disk()
    {
        Save("aaaaaa", Noon, 1);
        Save("bbbbbb", Noon.AddMinutes(4), 2);
        Save("cccccc", Noon.AddMinutes(8), 3);

        var restarted = new GuestNumbers(_archive);

        Assert.Equal(4, restarted.Next(Noon.AddMinutes(12)));
    }

    [Fact]
    public void Another_days_numbers_do_not_count()
    {
        Save("aaaaaa", Noon.AddDays(-1), 40);

        Assert.Equal(1, _numbers.Next(Noon));
    }

    /// <summary>
    /// Sessions from before numbers existed, or taken with them off, hold no number
    /// and must not stop the count.
    /// </summary>
    [Fact]
    public void Sessions_with_no_number_are_ignored()
    {
        Save("aaaaaa", Noon, null);

        Assert.Equal(1, _numbers.Next(Noon.AddMinutes(2)));
    }

    [Fact]
    public void Deleting_the_test_sessions_and_rescanning_starts_again_from_one()
    {
        var test = Save("aaaaaa", Noon, 1);
        Assert.Equal(2, _numbers.Next(Noon.AddMinutes(1)));

        Directory.Delete(_archive.FolderFor(test), recursive: true);
        _numbers.Rescan();

        Assert.Equal(1, _numbers.Next(Noon.AddMinutes(2)));
    }

    [Fact]
    public void Numbers_are_never_repeated_when_asked_for_at_the_same_time()
    {
        var taken = new int[200];

        Parallel.For(0, taken.Length, i => taken[i] = _numbers.Next(Noon));

        Assert.Equal(taken.Length, taken.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, taken.Length), taken.OrderBy(n => n));
    }

    [Fact]
    public void The_number_is_kept_in_the_session_record()
    {
        var saved = Save("aaaaaa", Noon, 7);

        var read = _archive.ByToken("aaaaaa");

        Assert.Equal(7, saved.Number);
        Assert.Equal(7, read!.Number);
    }

    /// <summary>
    /// Every session.json written before numbers existed has no such field, and
    /// the booth reads all of them on every poll.
    /// </summary>
    [Fact]
    public void A_record_written_before_numbers_existed_still_loads()
    {
        var saved = Save("aaaaaa", Noon, 7);
        var path = Path.Combine(_archive.FolderFor(saved), "session.json");

        // Removed as JSON, not as text: the file is indented with the platform's
        // line ending, so a string edit that works on a Mac misses on Windows.
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.True(json.Remove("number"), "the record should have been written with a number");
        File.WriteAllText(path, json.ToJsonString());

        var read = _archive.ByToken("aaaaaa");

        Assert.NotNull(read);
        Assert.Null(read.Number);
    }

    [Fact]
    public void The_day_of_a_session_is_the_first_part_of_its_folder_name()
    {
        var saved = Save("aaaaaa", Noon, 1);

        Assert.Equal("2026-10-28", SessionArchive.DayOf(saved));
        Assert.Equal("2026-10-28", SessionArchive.DayOf(Noon));
    }
}
