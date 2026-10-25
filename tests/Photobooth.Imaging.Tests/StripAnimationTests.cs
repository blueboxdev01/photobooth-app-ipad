using Microsoft.Extensions.Logging.Abstractions;
using Photobooth.Core;
using Photobooth.Imaging;
using SkiaSharp;

namespace Photobooth.Imaging.Tests;

/// <summary>
/// The looping video of a strip.
///
/// <para>
/// A session has three or four stills and no burst capture, so the motion comes
/// from rotating the photos through the slots. These tests state that rotation
/// as a property rather than checking pixels against a reference image: the
/// claim is <b>every photo appears in every slot exactly once over the loop, and
/// no two slots ever show the same photo</b>, and that is exactly what an
/// off-by-one or a wrong modulo breaks.
/// </para>
/// </summary>
public sealed class StripAnimationTests : IDisposable
{
    private readonly string _work =
        Path.Combine(Path.GetTempPath(), "pb-anim", Guid.NewGuid().ToString("N"));

    private readonly StripCompositor _compositor =
        new(NullLogger<StripCompositor>.Instance);

    public StripAnimationTests() => Directory.CreateDirectory(_work);

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Three slots in a row, big enough that the canvas gets shrunk.</summary>
    private static StripTemplate ThreeAcross() => new(
        "anim-test",
        new TemplateCanvas(2000, 600),
        [
            new TemplateSlot(0, 0, 1 / 3.0, 1),
            new TemplateSlot(1 / 3.0, 0, 1 / 3.0, 1),
            new TemplateSlot(2 / 3.0, 0, 1 / 3.0, 1),
        ]);

    private static readonly SKColor[] Palette = [SKColors.Red, SKColors.Lime, SKColors.Blue];

    /// <summary>One flat colour per photo, so which photo is where is unmistakable.</summary>
    private List<string> SolidPhotos()
    {
        var paths = new List<string>();

        for (var i = 0; i < Palette.Length; i++)
        {
            using var bitmap = new SKBitmap(400, 300, SKColorType.Rgba8888, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(Palette[i]);
            }

            var path = Path.Combine(_work, $"solid-{i}.png");
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(path);
            data.SaveTo(file);
            paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// The frames of one pass of the loop. Tested here rather than through the
    /// encoder so these run on a machine with no ffmpeg; the encoder has its own
    /// tests, which skip themselves when it is missing.
    /// </summary>
    private List<SKBitmap> Frames(AnimationOptions? options = null) =>
        _compositor.RenderAnimationFrames(
            ThreeAcross(), SolidPhotos(), _work, options ?? AnimationOptions.Default);

    /// <summary>
    /// Which of the three source colours a pixel is. Classified by dominant
    /// channel rather than exact equality, so a change of resampler cannot make
    /// these tests fail for a reason that has nothing to do with the rotation.
    /// </summary>
    private static int ColourIndex(SKColor pixel)
    {
        if (pixel.Red > pixel.Green && pixel.Red > pixel.Blue) return 0;   // red
        if (pixel.Green > pixel.Red && pixel.Green > pixel.Blue) return 1; // green
        if (pixel.Blue > pixel.Red && pixel.Blue > pixel.Green) return 2;  // blue
        return -1;
    }

    /// <summary>The colour in each slot, for each frame.</summary>
    private static int[][] SlotColours(IReadOnlyList<SKBitmap> frames, StripTemplate template)
    {
        var canvas = StripCompositor.Shrink(template.Canvas, AnimationOptions.Default.LongestEdge);

        var result = new int[frames.Count][];

        for (var f = 0; f < frames.Count; f++)
        {
            var colours = new int[template.Slots.Count];

            for (var s = 0; s < template.Slots.Count; s++)
            {
                var (x, y, w, h) = template.Slots[s].ToPixels(canvas);
                colours[s] = ColourIndex(frames[f].GetPixel(x + (w / 2), y + (h / 2)));
            }

            result[f] = colours;
        }

        return result;
    }

    // --- the rotation ---------------------------------------------------------

    /// <summary>
    /// Two slots showing the same photo at once is what a wrong modulo produces,
    /// and it would look obviously broken on the guest's screen.
    /// </summary>
    [Fact]
    public void No_two_slots_ever_show_the_same_photo()
    {
        var bitmaps = Frames();

        var frames = SlotColours(bitmaps, ThreeAcross());

        for (var f = 0; f < frames.Length; f++)
        {
            Assert.DoesNotContain(-1, frames[f]);
            Assert.True(frames[f].Distinct().Count() == frames[f].Length,
                $"frame {f + 1} shows slots {string.Join(", ", frames[f])} -- "
                + "two slots are displaying the same photo at the same moment.");
        }
    }

    /// <summary>
    /// The other half of the claim: over one loop, each slot shows every photo.
    /// Without this, a "rotation" that only swapped two of three would pass the
    /// test above while leaving one photo never seen in one slot.
    /// </summary>
    [Fact]
    public void Every_photo_visits_every_slot_exactly_once()
    {
        var template = ThreeAcross();
        var bitmaps = Frames();

        var frames = SlotColours(bitmaps, template);

        for (var slot = 0; slot < template.Slots.Count; slot++)
        {
            var seen = frames.Select(f => f[slot]).OrderBy(c => c).ToArray();

            Assert.Equal(Enumerable.Range(0, Palette.Length).ToArray(), seen);
        }
    }

    /// <summary>
    /// Frame one is the printed strip exactly, so the loop opens on the picture
    /// the guest has already been handed rather than a shuffled version of it.
    /// </summary>
    [Fact]
    public void The_first_frame_is_the_strip_as_printed()
    {
        var template = ThreeAcross();
        var bitmaps = Frames();

        Assert.Equal([0, 1, 2], SlotColours(bitmaps, template)[0]);
    }

    // --- the frames -----------------------------------------------------------

    [Fact]
    public void There_is_one_frame_per_photo()
    {
        Assert.Equal(Palette.Length, Frames().Count);
    }

    /// <summary>
    /// Shrunk to keep it downloadable over the booth's own wifi. 2000x600 at a
    /// 1000px limit is 1000x300.
    /// </summary>
    [Fact]
    public void The_animation_is_smaller_than_the_printed_strip()
    {
        var frame = Frames()[0];

        Assert.Equal(1000, frame.Width);
        Assert.Equal(300, frame.Height);
    }

    // --- the clip -------------------------------------------------------------

    /// <summary>
    /// A video, unlike a GIF, does not loop by itself once it has been posted,
    /// and Instagram refuses anything under three seconds. A cycle of three
    /// photos at 400ms is 1.2s, so it has to be repeated to reach the minimum.
    /// </summary>
    [Fact]
    public void The_cycle_is_repeated_until_the_clip_is_long_enough()
    {
        var list = Mp4Writer.ConcatList(3, new AnimationOptions(FrameDelayMilliseconds: 400, MinimumSeconds: 6));

        // 6s / 1.2s per cycle = 5 cycles of 3 frames, plus the closing entry.
        Assert.Equal(15, list.Split('\n').Count(l => l.StartsWith("duration ")));
        Assert.Equal(16, list.Split('\n').Count(l => l.StartsWith("file ")));
    }

    /// <summary>
    /// ffmpeg ignores the duration of the final entry, so the last photo needs
    /// listing once more or it flashes past for a single frame.
    /// </summary>
    [Fact]
    public void The_last_frame_is_listed_again_without_a_duration()
    {
        var lines = Mp4Writer.ConcatList(3, AnimationOptions.Default)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("file ", lines[^1]);
        Assert.StartsWith("duration ", lines[^2]);
        Assert.Equal(lines[^1], lines[^3]);
    }

    [Fact]
    public void Each_frame_is_held_for_the_requested_time()
    {
        var list = Mp4Writer.ConcatList(3, new AnimationOptions(FrameDelayMilliseconds: 500));

        Assert.Contains("duration 0.5\n", list);
    }

    /// <summary>
    /// The one test that runs the real encoder, and the one that would have
    /// caught a file every laptop plays and every phone refuses. Skips itself
    /// where ffmpeg is not installed rather than failing a build for it; the
    /// release workflow checks the bundled copy separately.
    /// </summary>
    [Fact]
    public void The_encoded_clip_is_an_h264_mp4_with_even_sides()
    {
        var ffmpeg = Mp4Writer.FindFfmpeg(null);
        if (!CanRun(ffmpeg))
        {
            return;
        }

        var path = Path.Combine(_work, "strip.mp4");
        _compositor.ComposeAnimation(ThreeAcross(), SolidPhotos(), _work, path);

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 1000, "the video is suspiciously small");

        // An MP4 starts with a box whose type is "ftyp" at offset 4.
        Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(bytes, 4, 4));
    }

    private static bool CanRun(string ffmpeg)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(ffmpeg, "-version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            process!.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A JPEG of a given size, with enough detail that the encoder cannot
    /// collapse it. <b>JPEG specifically</b>: scaling on the way out of the
    /// decoder is a property of its DCT blocks, and a PNG would come back at
    /// full size and quietly fail the claim being made here.
    /// </summary>
    private string Photo(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.DarkSlateGray };
            for (var y = 0; y < height; y += 16)
            {
                canvas.DrawRect(0, y, width, 8, paint);
            }
        }

        var path = Path.Combine(_work, $"photo-{width}x{height}.jpg");
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 92);
        using var file = File.Create(path);
        data.SaveTo(file);
        return path;
    }

    // --- what gets decoded ----------------------------------------------------

    /// <summary>
    /// The animation must not unpack the camera’s full resolution for a slot a
    /// few hundred pixels wide.
    ///
    /// <para>
    /// It used to, once per frame, so a four-shot session did sixteen
    /// full-resolution decodes and took five and a half seconds -- long enough
    /// to stall the countdown going out to the screens. This pins the scaled
    /// decode that replaced it: smaller than the source, but still with room to
    /// spare above what it will be drawn at, so the resampler has detail to
    /// average rather than exactly enough pixels and no more.
    /// </para>
    /// </summary>
    [Fact]
    public void A_photo_is_decoded_no_larger_than_it_will_be_drawn()
    {
        // 2400x1800, standing in for a camera frame.
        var big = Photo(2400, 1800);

        using var full = StripCompositor.DecodeOne(big, 0);
        using var scaled = StripCompositor.DecodeOne(big, 300);

        Assert.NotNull(full);
        Assert.NotNull(scaled);

        Assert.Equal(2400, full!.Width);

        Assert.True(scaled!.Width < full.Width,
            $"asked for 300px and got the whole {scaled.Width}px source back");

        // Never below what it is drawn at, or the fix would have traded the
        // slowness for the softness we spent a release removing.
        Assert.True(scaled.Width >= 300,
            $"decoded to {scaled.Width}px for a 300px slot, which is too little to "
            + "downscale from cleanly");
    }

    /// <summary>A source already smaller than the slot is left alone.</summary>
    [Fact]
    public void A_small_photo_is_not_scaled_up_on_the_way_in()
    {
        var small = Photo(200, 150);

        using var decoded = StripCompositor.DecodeOne(small, 400);

        Assert.NotNull(decoded);
        Assert.Equal(200, decoded!.Width);
    }

    // --- shrinking ------------------------------------------------------------

    [Theory]
    [InlineData(600, 1800, 333, 1000)]   // the classic 2x6 strip
    [InlineData(1800, 1200, 1000, 667)]  // a 6x4 landscape
    [InlineData(800, 400, 800, 400)]     // already small: left alone, never enlarged
    public void Shrinking_keeps_the_shape(int w, int h, int expectedW, int expectedH)
    {
        var shrunk = StripCompositor.Shrink(new TemplateCanvas(w, h), 1000);

        Assert.Equal(expectedW, shrunk.Width);
        Assert.Equal(expectedH, shrunk.Height);
    }
}
