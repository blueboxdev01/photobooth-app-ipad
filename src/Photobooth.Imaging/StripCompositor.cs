using Microsoft.Extensions.Logging;
using Photobooth.Core;
using SkiaSharp;

namespace Photobooth.Imaging;

/// <summary>How the looping video of a strip is built.</summary>
/// <param name="LongestEdge">
/// The animation's longest side in pixels. Deliberately far smaller than the
/// printed strip: this is downloaded to a phone over the booth's own wifi, and
/// a full-size video is a large download for no visible gain on a phone screen.
/// </param>
/// <param name="FrameDelayMilliseconds">
/// How long each photo is held. Around 400ms reads as a photobooth loop; much
/// faster and it is a flicker nobody can follow.
/// </param>
/// <param name="MinimumSeconds">
/// The least the video runs for. A strip is three or four photos at a few
/// hundred milliseconds each, which is under two seconds -- and a video file,
/// unlike a GIF, does not loop by itself once it has been posted. Instagram will
/// not take anything under three seconds, so the cycle is repeated to fill
/// this.
/// </param>
/// <param name="FfmpegPath">
/// Where ffmpeg is. Null looks next to the app, then on the PATH.
/// </param>
public sealed record AnimationOptions(
    int LongestEdge = 1000,
    int FrameDelayMilliseconds = 400,
    int MinimumSeconds = 6,
    string? FfmpegPath = null)
{
    public static AnimationOptions Default { get; } = new();
}

/// <summary>
/// Draws the finished strip: background, photos into their slots, then the frame
/// art on top. Also the looping video of the same thing.
/// </summary>
public sealed class StripCompositor(ILogger<StripCompositor> logger)
{
    /// <summary>
    /// How photos and art are resampled into the canvas.
    ///
    /// <para>
    /// Not optional, and not a default worth accepting. A 6000x4000 frame landing
    /// in a 560x420 slot is close to a <b>tenfold reduction</b>, and Skia's
    /// default sampling is nearest-neighbour: it keeps roughly one source pixel
    /// in ninety and averages nothing at all. That is what made every strip look
    /// soft and faintly crunchy beside the very photos it was built from.
    /// </para>
    ///
    /// <para>
    /// Mipmapping is what makes a reduction this large correct. It pre-filters
    /// the source in halving steps so every source pixel contributes to the
    /// result instead of most being thrown away, and linear filtering blends
    /// between those steps. At 1:1 it uses the full-size level, so art that
    /// already matches the canvas is copied untouched.
    /// </para>
    ///
    /// <para>
    /// Worth knowing if this ever reads as wrong again: <c>SKPaint.IsAntialias</c>
    /// does <b>not</b> control this. It affects geometry edge coverage, not image
    /// resampling, which is exactly why the old code looked like it was handling
    /// quality while it was not. <c>SKPaint.FilterQuality</c>, which did control
    /// it, was removed in SkiaSharp 4.
    /// </para>
    /// </summary>
    private static readonly SKSamplingOptions HighQuality =
        new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// Composites <paramref name="photoPaths"/> into the template and writes a JPEG.
    /// </summary>
    /// <param name="templateFolder">
    /// Where the overlay named by the template lives.
    /// </param>
    /// <param name="stamp">
    /// Small text printed on the strip, such as the guest's pickup number. Null
    /// leaves the strip as the template draws it.
    /// </param>
    public void Compose(
        StripTemplate template,
        IReadOnlyList<string> photoPaths,
        string templateFolder,
        string outputPath,
        int jpegQuality = 95,
        string? stamp = null)
    {
        // Full resolution: this is the keepsake and the thing that gets printed,
        // so the slot gets every pixel the camera gave it.
        var photos = Decode(photoPaths, longestEdgeNeeded: 0);
        using var art = LoadArt(template, templateFolder, longestEdgeNeeded: 0);

        try
        {
            using var surface = Render(template, photos, art, SKAlphaType.Premul, stamp);
            Write(surface, template, outputPath, jpegQuality);
        }
        finally
        {
            Dispose(photos);
        }

        logger.LogInformation(
            "Composed {Output} ({Width}x{Height} at {Dpi} DPI, {Slots} photos, art {Art})",
            Path.GetFileName(outputPath), template.Canvas.Width, template.Canvas.Height,
            template.Canvas.Dpi, template.Slots.Count, template.Art);
    }

    /// <summary>
    /// The strip as it would print, small enough to put on a screen while the
    /// guest decides whether to keep it.
    ///
    /// <para>
    /// Drawn by the same <see cref="Render"/> as the printed strip and the video,
    /// so it cannot disagree with either about framing, cropping or art. It is
    /// rendered on a shrunken canvas from photos decoded at a fraction of their
    /// size, which is what keeps it well under a second on a 24 MP camera --
    /// the preview is on screen while the operator waits to press Accept, and a
    /// preview slower than the operator is no preview.
    /// </para>
    /// </summary>
    /// <returns>A JPEG.</returns>
    public byte[] ComposePreview(
        StripTemplate template,
        IReadOnlyList<string> photoPaths,
        string templateFolder,
        int longestEdge = 1200,
        int jpegQuality = 85)
    {
        var scaled = template with { Canvas = Shrink(template.Canvas, longestEdge) };

        var photos = Decode(photoPaths, BiggestSlot(scaled, longestEdge));
        using var art = LoadArt(scaled, templateFolder, longestEdge);

        try
        {
            using var surface = Render(scaled, photos, art, SKAlphaType.Premul);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
            return data.ToArray();
        }
        finally
        {
            Dispose(photos);
        }
    }

    /// <summary>The longest side, in pixels, of the largest slot on this canvas.</summary>
    private static int BiggestSlot(StripTemplate scaled, int fallback) =>
        scaled.Slots
            .Select(slot => slot.ToPixels(scaled.Canvas))
            .Select(px => Math.Max(px.W, px.H))
            .DefaultIfEmpty(fallback)
            .Max();

    /// <summary>
    /// Writes a looping MP4 of the same strip, with the photos moving.
    ///
    /// <para>
    /// A session has three or four stills and no burst capture, so the motion
    /// has to come from the photos themselves: frame <i>k</i> puts photo
    /// <c>(i + k) % n</c> in slot <i>i</i>, rotating them through the slots.
    /// Every slot always holds a different photo, every photo is on screen the
    /// whole time, and the frame art stays put. Frame zero is the printed strip
    /// exactly, so the animation starts on the picture the guest already knows.
    /// </para>
    ///
    /// <para>
    /// Never fatal to a session. The caller treats a failure here as "no
    /// animation", because the strip and the photos are the thing that matters
    /// and a video is not worth costing a guest their session.
    /// </para>
    /// </summary>
    public void ComposeAnimation(
        StripTemplate template,
        IReadOnlyList<string> photoPaths,
        string templateFolder,
        string outputPath,
        AnimationOptions? options = null,
        string? stamp = null)
    {
        var settings = options ?? AnimationOptions.Default;

        var frames = RenderAnimationFrames(template, photoPaths, templateFolder, settings, stamp);
        var (width, height, count) = (frames[0].Width, frames[0].Height, frames.Count);

        try
        {
            Mp4Writer.Write(frames, outputPath, settings);
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }

        logger.LogInformation(
            "Composed {Output} ({Width}x{Height}, {Frames} frames at {Delay}ms)",
            Path.GetFileName(outputPath), width, height,
            count, settings.FrameDelayMilliseconds);
    }

    /// <summary>
    /// The frames of one pass of the loop, in order. The caller owns and disposes
    /// them. Split from the encoder so the rotation can be tested without
    /// ffmpeg, which is the part that is easy to get subtly wrong.
    /// </summary>
    internal List<SKBitmap> RenderAnimationFrames(
        StripTemplate template,
        IReadOnlyList<string> photoPaths,
        string templateFolder,
        AnimationOptions settings,
        string? stamp = null)
    {
        // Rendered small rather than at print size. Slots are stored as fractions
        // of the canvas precisely so a template survives a change of output size,
        // so shrinking the canvas carries the slots and the art with it for free
        // -- and a full-size video would be a large download for something a
        // guest watches on a phone over the booth's own wifi.
        var scaled = template with
        {
            Canvas = Shrink(template.Canvas, settings.LongestEdge),
        };

        var frames = new List<SKBitmap>(photoPaths.Count);

        // Decoded once, here, and reused for every frame.
        //
        // This used to decode inside the frame loop, which meant n frames x n
        // slots = n-squared decodes of whatever the camera produced. On a
        // 6000x4000 Canon file a four-shot session did sixteen full-resolution
        // JPEG decodes and took five and a half seconds -- long enough to stall
        // the countdown pushes going out to the screens.
        //
        // And decoded small. A slot in a 1000px animation is a few hundred
        // pixels wide, so there is nothing to gain from unpacking twenty-four
        // megapixels to fill it. JPEG scales natively on the way out of the
        // decoder, which is far cheaper than doing it afterwards.
        var biggestSlot = BiggestSlot(scaled, settings.LongestEdge);

        var photos = Decode(photoPaths, biggestSlot);
        using var art = LoadArt(scaled, templateFolder, settings.LongestEdge);

        try
        {
            for (var frame = 0; frame < photoPaths.Count; frame++)
            {
                var rotated = new SKBitmap?[photoPaths.Count];
                for (var slot = 0; slot < photoPaths.Count; slot++)
                {
                    rotated[slot] = photos[(slot + frame) % photoPaths.Count];
                }

                using var surface = Render(scaled, rotated, art, SKAlphaType.Opaque, stamp);
                using var image = surface.Snapshot();

                // Read into a bitmap of a colour type we have named, rather than
                // whatever the platform would have picked. Windows defaults to
                // BGRA; naming the layout means the encoder is never handed
                // pixels in an order it did not expect.
                var info = new SKImageInfo(
                    scaled.Canvas.Width, scaled.Canvas.Height,
                    SKColorType.Rgba8888, SKAlphaType.Opaque);

                var bitmap = new SKBitmap(info);
                if (!image.ReadPixels(info, bitmap.GetPixels(), info.RowBytes, 0, 0))
                {
                    bitmap.Dispose();
                    throw new InvalidOperationException(
                        $"Could not read frame {frame + 1} of the animation.");
                }

                frames.Add(bitmap);
            }
        }
        catch
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }

            throw;
        }
        finally
        {
            Dispose(photos);
        }

        return frames;
    }

    /// <summary>
    /// The canvas scaled down so its longest edge is at most
    /// <paramref name="longestEdge"/>, keeping its shape. Never scales up.
    /// </summary>
    internal static TemplateCanvas Shrink(TemplateCanvas canvas, int longestEdge)
    {
        var longest = Math.Max(canvas.Width, canvas.Height);
        if (longest <= longestEdge)
        {
            return canvas;
        }

        var factor = longestEdge / (double)longest;
        return new TemplateCanvas(
            Math.Max(1, (int)Math.Round(canvas.Width * factor)),
            Math.Max(1, (int)Math.Round(canvas.Height * factor)),
            canvas.Dpi);
    }

    /// <summary>
    /// Draw one complete frame: background, art behind, the photos in slot order,
    /// art in front.
    ///
    /// Shared by the printed strip and by every frame of the animation, so the
    /// two can never drift apart -- an animation that framed or cropped photos
    /// differently from the strip it claims to be a copy of would be worse than
    /// no animation.
    /// </summary>
    /// <param name="photoPaths">One per slot, in slot order.</param>
    private SKSurface Render(
        StripTemplate template,
        IReadOnlyList<SKBitmap?> photos,
        SKBitmap? art,
        SKAlphaType alphaType,
        string? stamp = null)
    {
        if (photos.Count != template.Slots.Count)
        {
            // A mismatch means the session and the template disagree about how many
            // photos there are, which should be impossible now that the template
            // decides the shot count -- so fail loudly rather than half-fill a strip.
            throw new ArgumentException(
                $"Template '{template.Name}' has {template.Slots.Count} slots but " +
                $"{photos.Count} photos were supplied.", nameof(photos));
        }

        var canvasInfo = new SKImageInfo(
            template.Canvas.Width, template.Canvas.Height, SKColorType.Rgba8888, alphaType);

        var surface = SKSurface.Create(canvasInfo);
        var canvas = surface.Canvas;
        canvas.Clear(ParseColour(template.Background));

        // A backdrop goes down before the photos so they sit on top of it; a frame
        // goes over them and shows them through its transparent windows.
        if (template.Art == ArtLayer.Behind)
        {
            DrawArt(canvas, template, art);
        }

        for (var i = 0; i < template.Slots.Count; i++)
        {
            DrawSlot(canvas, template, template.Slots[i], photos[i]);
        }

        if (template.Art == ArtLayer.InFront)
        {
            DrawArt(canvas, template, art);
        }

        // Last, so the art can never cover it.
        if (!string.IsNullOrWhiteSpace(stamp))
        {
            DrawStamp(canvas, template.Canvas, stamp);
        }

        return surface;
    }

    /// <summary>
    /// A small label at the foot of the strip, on a white plate so it reads over
    /// any frame art. Sized from the canvas width, so it keeps its proportions on
    /// the printed strip, the on-screen preview and the shrunken video frames.
    /// </summary>
    private static void DrawStamp(SKCanvas canvas, TemplateCanvas size, string text)
    {
        var fontSize = size.Width * 0.065f;

        using var typeface = SKTypeface.FromFamilyName("Helvetica", SKFontStyle.Bold)
            ?? SKTypeface.Default;
        using var font = new SKFont(typeface, fontSize);
        using var ink = new SKPaint { Color = new SKColor(0x14, 0x16, 0x1A), IsAntialias = true };
        using var plate = new SKPaint { Color = SKColors.White.WithAlpha(235), IsAntialias = true };

        var width = font.MeasureText(text);
        var padX = fontSize * 0.55f;
        var padY = fontSize * 0.35f;
        var margin = size.Width * 0.035f;

        var plateRect = new SKRect(
            (size.Width - width) / 2 - padX,
            size.Height - margin - fontSize - 2 * padY,
            (size.Width + width) / 2 + padX,
            size.Height - margin);

        canvas.DrawRoundRect(plateRect, padY, padY, plate);
        canvas.DrawText(
            text, size.Width / 2f, plateRect.Bottom - padY - fontSize * 0.12f,
            SKTextAlign.Center, font, ink);
    }

    private static void DrawSlot(
        SKCanvas canvas, StripTemplate template, TemplateSlot slot, SKBitmap? bitmap)
    {
        // Null when that photo could not be decoded. The slot is left as
        // background rather than failing the whole strip for one bad file.
        if (bitmap is null)
        {
            return;
        }

        var (x, y, w, h) = slot.ToPixels(template.Canvas);
        var target = new SKRect(x, y, x + w, y + h);

        var source = slot.Fit == SlotFit.Cover
            ? CoverCrop(bitmap.Width, bitmap.Height, w / (float)h)
            : new SKRect(0, 0, bitmap.Width, bitmap.Height);

        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(bitmap, source, target, HighQuality, paint);
    }

    /// <summary>
    /// The centre region of the photo matching the slot's aspect ratio.
    ///
    /// This is where the R50's 3:2 frame loses its sides to a 4:3 slot -- the
    /// reason the guest screen shows a crop guide rather than the camera's full
    /// frame.
    /// </summary>
    internal static SKRect CoverCrop(int width, int height, float targetAspect)
    {
        var sourceAspect = width / (float)height;

        if (sourceAspect > targetAspect)
        {
            // Source is wider: trim the left and right.
            var cropWidth = height * targetAspect;
            var sideInset = (width - cropWidth) / 2f;
            return new SKRect(sideInset, 0, width - sideInset, height);
        }

        // Source is taller: trim the top and bottom.
        var cropHeight = width / targetAspect;
        var topInset = (height - cropHeight) / 2f;
        return new SKRect(0, topInset, width, height - topInset);
    }

    private static void DrawArt(SKCanvas canvas, StripTemplate template, SKBitmap? overlay)
    {
        if (overlay is null)
        {
            return;
        }

        var full = new SKRect(0, 0, template.Canvas.Width, template.Canvas.Height);

        // Cover-cropped rather than stretched to the canvas. Art drawn at a
        // different aspect ratio used to come out visibly distorted; centre-
        // cropping loses the edges instead, which is the lesser sin and matches
        // how photos are fitted into their slots.
        var source = CoverCrop(
            overlay.Width, overlay.Height, template.Canvas.Width / (float)template.Canvas.Height);

        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(overlay, source, full, HighQuality, paint);
    }

    /// <summary>Encode the finished canvas and stamp its physical size.</summary>
    private static void Write(
        SKSurface surface, StripTemplate template, string outputPath, int jpegQuality)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using (var file = File.Create(outputPath))
        {
            data.SaveTo(file);
        }

        // Skia does not write JPEG density, and a strip that prints at the wrong
        // physical size is the single most likely printing complaint. Patch the
        // JFIF header so 600x1800 really means 2x6 inches at 300 DPI.
        JpegDensity.Stamp(outputPath, template.Canvas.Dpi);
    }

    private List<SKBitmap?> Decode(IReadOnlyList<string> paths, int longestEdgeNeeded)
    {
        var decoded = new List<SKBitmap?>(paths.Count);

        foreach (var path in paths)
        {
            var bitmap = DecodeOne(path, longestEdgeNeeded);
            if (bitmap is null)
            {
                logger.LogWarning("Could not decode {Photo}; leaving its slot empty.", path);
            }

            decoded.Add(bitmap);
        }

        return decoded;
    }

    /// <summary>
    /// One photo, decoded no larger than it needs to be.
    ///
    /// <para>
    /// A JPEG decoder can scale on the way out, and doing so costs a fraction of
    /// unpacking every pixel and throwing most of them away. That matters here:
    /// a slot in the animation is a few hundred pixels wide and the camera hands
    /// us twenty-four megapixels.
    /// </para>
    /// </summary>
    /// <param name="longestEdgeNeeded">
    /// The largest the image will ever be drawn, or 0 to decode it whole -- which
    /// is what the printed strip asks for. Twice this is requested, so the
    /// resampler still has detail to average rather than being handed exactly the
    /// pixels it needs and no spare.
    /// </param>
    internal static SKBitmap? DecodeOne(string path, int longestEdgeNeeded)
    {
        if (longestEdgeNeeded <= 0)
        {
            return SKBitmap.Decode(path);
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);

            if (codec is null)
            {
                return SKBitmap.Decode(path);
            }

            var longest = Math.Max(codec.Info.Width, codec.Info.Height);
            if (longest <= longestEdgeNeeded * 2)
            {
                return SKBitmap.Decode(path);
            }

            // The codec rounds this to a size it can actually produce -- for JPEG,
            // a half, a quarter, an eighth -- so ask and then believe the answer.
            var wanted = Math.Min(1f, (longestEdgeNeeded * 2f) / longest);
            var dimensions = codec.GetScaledDimensions(wanted);

            var info = new SKImageInfo(
                dimensions.Width, dimensions.Height,
                SKColorType.Rgba8888, SKAlphaType.Premul);

            var bitmap = new SKBitmap(info);
            if (codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success)
            {
                return bitmap;
            }

            bitmap.Dispose();
        }
        catch (Exception)
        {
            // A scaled decode is an optimisation, never a reason to lose a photo.
        }

        return SKBitmap.Decode(path);
    }

    /// <summary>The template’s art, or null when it has none or it cannot be read.</summary>
    private SKBitmap? LoadArt(StripTemplate template, string templateFolder, int longestEdgeNeeded)
    {
        if (string.IsNullOrWhiteSpace(template.Overlay))
        {
            return null;
        }

        var path = Path.Combine(templateFolder, template.Overlay);
        if (!File.Exists(path))
        {
            logger.LogWarning("Art {Art} not found; the strip will have none.", path);
            return null;
        }

        var art = DecodeOne(path, longestEdgeNeeded);
        if (art is null)
        {
            logger.LogWarning("Art {Art} could not be decoded.", path);
        }

        return art;
    }

    private static void Dispose(IEnumerable<SKBitmap?> bitmaps)
    {
        foreach (var bitmap in bitmaps)
        {
            bitmap?.Dispose();
        }
    }

    private static SKColor ParseColour(string value) =>
        SKColor.TryParse(value, out var colour) ? colour : SKColors.White;
}
