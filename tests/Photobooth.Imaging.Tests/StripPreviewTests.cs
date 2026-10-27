using Microsoft.Extensions.Logging.Abstractions;
using Photobooth.Core;
using SkiaSharp;

namespace Photobooth.Imaging.Tests;

/// <summary>
/// The strip drawn for the guest to look over before it is accepted.
///
/// <para>
/// It must show the strip that will be printed -- same photos in the same slots,
/// same art -- just smaller. Checked by colour: each photo is one flat colour, so
/// where it landed is unmistakable.
/// </para>
/// </summary>
public sealed class StripPreviewTests : IDisposable
{
    private readonly string _work =
        Path.Combine(Path.GetTempPath(), "pb-preview", Guid.NewGuid().ToString("N"));

    private readonly StripCompositor _compositor =
        new(NullLogger<StripCompositor>.Instance);

    public StripPreviewTests() => Directory.CreateDirectory(_work);

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    private static StripTemplate ThreeDown() => new(
        "preview-test",
        new TemplateCanvas(600, 1800),
        [
            new TemplateSlot(0, 0, 1, 1 / 3.0),
            new TemplateSlot(0, 1 / 3.0, 1, 1 / 3.0),
            new TemplateSlot(0, 2 / 3.0, 1, 1 / 3.0),
        ]);

    private List<string> SolidPhotos(params SKColor[] colours)
    {
        var paths = new List<string>();

        for (var i = 0; i < colours.Length; i++)
        {
            using var bitmap = new SKBitmap(600, 400, SKColorType.Rgba8888, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(colours[i]);
            }

            var path = Path.Combine(_work, $"solid-{i}.jpg");
            using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95);
            File.WriteAllBytes(path, data.ToArray());
            paths.Add(path);
        }

        return paths;
    }

    private static SKColor CentreOf(SKBitmap strip, TemplateSlot slot)
    {
        var (x, y, w, h) = slot.ToPixels(new TemplateCanvas(strip.Width, strip.Height));
        return strip.GetPixel(x + (w / 2), y + (h / 2));
    }

    [Fact]
    public void The_preview_is_a_small_jpeg()
    {
        var bytes = _compositor.ComposePreview(
            ThreeDown(), SolidPhotos(SKColors.Red, SKColors.Lime, SKColors.Blue), _work, longestEdge: 900);

        using var strip = SKBitmap.Decode(bytes);

        Assert.NotNull(strip);
        Assert.Equal(900, strip.Height);
        Assert.Equal(300, strip.Width);
    }

    [Fact]
    public void The_preview_keeps_the_shape_of_the_printed_strip()
    {
        var bytes = _compositor.ComposePreview(
            ThreeDown(), SolidPhotos(SKColors.Red, SKColors.Lime, SKColors.Blue), _work, longestEdge: 900);

        using var strip = SKBitmap.Decode(bytes);

        Assert.Equal(600 / 1800.0, strip.Width / (double)strip.Height, precision: 2);
    }

    [Fact]
    public void Each_photo_lands_in_its_own_slot()
    {
        var template = ThreeDown();

        var bytes = _compositor.ComposePreview(
            template, SolidPhotos(SKColors.Red, SKColors.Lime, SKColors.Blue), _work, longestEdge: 900);

        using var strip = SKBitmap.Decode(bytes);

        var top = CentreOf(strip, template.Slots[0]);
        var middle = CentreOf(strip, template.Slots[1]);
        var bottom = CentreOf(strip, template.Slots[2]);

        Assert.True(top.Red > 200 && top.Green < 60 && top.Blue < 60, $"top was {top}");
        Assert.True(middle.Green > 200 && middle.Red < 60 && middle.Blue < 60, $"middle was {middle}");
        Assert.True(bottom.Blue > 200 && bottom.Red < 60 && bottom.Green < 60, $"bottom was {bottom}");
    }

    /// <summary>
    /// The guest is shown whatever order the operator has put the shots in, so
    /// reordering has to change the picture.
    /// </summary>
    [Fact]
    public void Putting_the_shots_in_a_different_order_changes_the_picture()
    {
        var template = ThreeDown();
        var photos = SolidPhotos(SKColors.Red, SKColors.Lime, SKColors.Blue);
        var reversed = Enumerable.Reverse(photos).ToList();

        using var forward = SKBitmap.Decode(_compositor.ComposePreview(template, photos, _work, 900));
        using var backward = SKBitmap.Decode(_compositor.ComposePreview(template, reversed, _work, 900));

        var firstForward = CentreOf(forward, template.Slots[0]);
        var firstBackward = CentreOf(backward, template.Slots[0]);

        Assert.True(firstForward.Red > 200);
        Assert.True(firstBackward.Blue > 200);
    }

    [Fact]
    public void A_canvas_already_small_enough_is_not_scaled_up()
    {
        var bytes = _compositor.ComposePreview(
            ThreeDown(), SolidPhotos(SKColors.Red, SKColors.Lime, SKColors.Blue), _work, longestEdge: 4000);

        using var strip = SKBitmap.Decode(bytes);

        Assert.Equal(1800, strip.Height);
    }

    /// <summary>
    /// The same loud failure as the printed strip: a template that wants three
    /// photos is not drawn from two.
    /// </summary>
    [Fact]
    public void The_wrong_number_of_photos_is_refused()
    {
        var photos = SolidPhotos(SKColors.Red, SKColors.Lime);

        Assert.Throws<ArgumentException>(() =>
            _compositor.ComposePreview(ThreeDown(), photos, _work));
    }
}
