using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The strip drawn for review. The risk is a screen showing a picture of shots
/// that are no longer the ones in the session, so these are about overtaking.
/// </summary>
public sealed class StripPreviewStoreTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xD9];

    [Fact]
    public void Nothing_is_offered_before_anything_has_been_drawn()
    {
        var store = new StripPreviewStore();

        Assert.Null(store.Current());
        Assert.Null(store.Bytes());
    }

    [Fact]
    public void A_finished_preview_is_offered_with_the_shots_it_was_drawn_from()
    {
        var store = new StripPreviewStore();
        Assert.True(store.Want("a|b|c"));

        var info = store.Complete("a|b|c", Jpeg);

        Assert.NotNull(info);
        Assert.Equal("a|b|c", info.Key);
        Assert.Equal(info, store.Current());
        Assert.Equal(Jpeg, store.Bytes());
    }

    [Fact]
    public void Asking_again_for_the_same_shots_does_not_redraw_them()
    {
        var store = new StripPreviewStore();

        Assert.True(store.Want("a|b|c"));
        Assert.False(store.Want("a|b|c"));
    }

    /// <summary>
    /// A reorder while the first picture is still being drawn: the slow one must
    /// not land afterwards and replace the right answer with a stale one.
    /// </summary>
    [Fact]
    public void A_slow_render_that_has_been_overtaken_is_thrown_away()
    {
        var store = new StripPreviewStore();
        store.Want("a|b|c");
        store.Want("c|b|a");

        Assert.Null(store.Complete("a|b|c", [1]));
        Assert.NotNull(store.Complete("c|b|a", [2]));

        Assert.Equal("c|b|a", store.Current()!.Key);
        Assert.Equal([2], store.Bytes());
    }

    [Fact]
    public void Each_new_preview_gets_a_new_address()
    {
        var store = new StripPreviewStore();
        store.Want("a");
        var first = store.Complete("a", Jpeg)!;
        store.Want("b");
        var second = store.Complete("b", Jpeg)!;

        Assert.NotEqual(first.Url, second.Url);
    }

    [Fact]
    public void A_failed_render_can_be_tried_again()
    {
        var store = new StripPreviewStore();
        store.Want("a|b|c");
        store.Failed("a|b|c");

        Assert.True(store.Want("a|b|c"));
    }

    [Fact]
    public void The_next_guest_starts_with_nothing()
    {
        var store = new StripPreviewStore();
        store.Want("a|b|c");
        store.Complete("a|b|c", Jpeg);

        store.Clear();

        Assert.Null(store.Current());
        Assert.Null(store.Bytes());
        Assert.True(store.Want("a|b|c"));
    }
}
