using Photobooth.Core;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The key the booth names a preview with, and the one the browsers build from
/// the snapshot, have to agree character for character -- or the guest screen
/// never shows the preview, with nothing to say why.
/// </summary>
public sealed class PreviewKeyTests
{
    private static CapturedPhoto Photo(string name) =>
        new($@"C:\watch\{name}", name, 1000, DateTimeOffset.UtcNow);

    [Fact]
    public void The_key_is_the_shots_in_strip_order()
    {
        var key = SessionCoordinator.PreviewKey([Photo("IMG_0001.JPG"), Photo("IMG_0002.JPG")]);

        Assert.Equal("IMG_0001.JPG|IMG_0002.JPG", key);
    }

    [Fact]
    public void A_different_order_is_a_different_key()
    {
        var a = SessionCoordinator.PreviewKey([Photo("IMG_0001.JPG"), Photo("IMG_0002.JPG")]);
        var b = SessionCoordinator.PreviewKey([Photo("IMG_0002.JPG"), Photo("IMG_0001.JPG")]);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void A_retaken_shot_is_a_different_key()
    {
        var before = SessionCoordinator.PreviewKey([Photo("IMG_0001.JPG"), Photo("IMG_0002.JPG")]);
        var after = SessionCoordinator.PreviewKey([Photo("IMG_0001.JPG"), Photo("IMG_0005.JPG")]);

        Assert.NotEqual(before, after);
    }
}
