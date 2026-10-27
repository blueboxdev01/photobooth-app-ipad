using Microsoft.Extensions.Time.Testing;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The guests still on their way to the claiming station.
///
/// Every entry is a photos link that a device on the guests' wifi can ask for, so
/// most of this is about the list staying short.
/// </summary>
public sealed class ClaimBoardTests
{
    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private readonly ClaimBoard _board;

    public ClaimBoardTests() => _board = new ClaimBoard(_time);

    private ClaimEntry Guest(string token) =>
        new(token, $"folder-{token}", $"/api/sessions/folder-{token}/strip.jpg", _time.GetUtcNow());

    [Fact]
    public void An_empty_board_has_nobody_on_it()
    {
        Assert.Empty(_board.Recent());
    }

    [Fact]
    public void The_newest_guest_comes_first()
    {
        _board.Add(Guest("a"));
        _time.Advance(TimeSpan.FromSeconds(30));
        _board.Add(Guest("b"));

        Assert.Equal(["b", "a"], _board.Recent().Select(g => g.Token));
    }

    [Fact]
    public void Only_the_last_few_guests_are_kept()
    {
        foreach (var token in new[] { "a", "b", "c", "d", "e" })
        {
            _board.Add(Guest(token));
        }

        var recent = _board.Recent();

        Assert.Equal(ClaimBoard.MaxEntries, recent.Count);
        Assert.Equal(["e", "d", "c"], recent.Select(g => g.Token));
    }

    [Fact]
    public void The_same_session_twice_is_one_entry()
    {
        _board.Add(Guest("a"));
        _board.Add(Guest("b"));
        _board.Add(Guest("a"));

        Assert.Equal(["a", "b"], _board.Recent().Select(g => g.Token));
    }

    [Fact]
    public void A_guest_leaves_the_board_after_a_few_minutes()
    {
        _board.Add(Guest("a"));
        _time.Advance(ClaimBoard.Lifetime - TimeSpan.FromSeconds(1));
        _board.Add(Guest("b"));

        Assert.Equal(2, _board.Recent().Count);

        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(["b"], _board.Recent().Select(g => g.Token));
    }

    [Fact]
    public void Everyone_eventually_leaves()
    {
        _board.Add(Guest("a"));
        _time.Advance(ClaimBoard.Lifetime + TimeSpan.FromSeconds(1));

        Assert.Empty(_board.Recent());
    }

    /// <summary>
    /// What the caller gets is a copy: a screen iterating it while the next guest
    /// is added must not see the list change underneath it.
    /// </summary>
    [Fact]
    public void The_list_handed_out_does_not_change_afterwards()
    {
        _board.Add(Guest("a"));
        var before = _board.Recent();

        _board.Add(Guest("b"));

        Assert.Single(before);
    }
}
