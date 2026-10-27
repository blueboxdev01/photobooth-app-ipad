using Photobooth.Core;
using SkiaSharp;

namespace Photobooth.Imaging.Tests;

/// <summary>
/// The small copies the guest's page and the guest display draw instead of the
/// camera's originals.
/// </summary>
public sealed class ThumbnailTests : IDisposable
{
    private readonly string _work =
        Path.Combine(Path.GetTempPath(), "pb-thumbs", Guid.NewGuid().ToString("N"));

    public ThumbnailTests() => Directory.CreateDirectory(_work);

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    private string Photo(int width, int height)
    {
        var path = Path.Combine(_work, $"photo-{width}x{height}.jpg");
        File.WriteAllBytes(path, ArtFixtures.Jpeg(new TemplateCanvas(width, height)));
        return path;
    }

    private static (int Width, int Height) Size(byte[] jpeg)
    {
        using var bitmap = SKBitmap.Decode(jpeg);
        return (bitmap.Width, bitmap.Height);
    }

    [Fact]
    public void A_large_photo_is_reduced_to_the_requested_longest_edge()
    {
        var bytes = Thumbnails.Render(Photo(3000, 2000), longestEdge: 640);

        Assert.NotNull(bytes);
        var (width, height) = Size(bytes);
        Assert.Equal(640, width);
        Assert.Equal(427, height);
    }

    [Fact]
    public void A_portrait_photo_keeps_its_shape()
    {
        var bytes = Thumbnails.Render(Photo(2000, 3000), longestEdge: 640);

        Assert.NotNull(bytes);
        var (width, height) = Size(bytes);
        Assert.Equal(427, width);
        Assert.Equal(640, height);
    }

    [Fact]
    public void A_photo_already_small_enough_is_not_scaled_up()
    {
        var bytes = Thumbnails.Render(Photo(300, 200), longestEdge: 640);

        Assert.NotNull(bytes);
        var (width, height) = Size(bytes);
        Assert.Equal(300, width);
        Assert.Equal(200, height);
    }

    [Fact]
    public void The_copy_is_a_fraction_of_the_size_of_the_original()
    {
        var original = Photo(3000, 2000);

        var bytes = Thumbnails.Render(original, longestEdge: 640);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length < new FileInfo(original).Length);
    }

    [Fact]
    public void A_file_that_is_not_an_image_gives_nothing_and_leaves_no_file()
    {
        var bad = Path.Combine(_work, "photo-1.jpg");
        File.WriteAllText(bad, "not a jpeg");
        var destination = Path.Combine(_work, "thumb-1.jpg");

        var written = Thumbnails.Write(bad, destination, longestEdge: 640);

        Assert.False(written);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void Write_puts_the_copy_where_it_was_asked()
    {
        var destination = Path.Combine(_work, "thumb-1.jpg");

        var written = Thumbnails.Write(Photo(3000, 2000), destination, longestEdge: 640);

        Assert.True(written);
        Assert.True(File.Exists(destination));
    }
}
