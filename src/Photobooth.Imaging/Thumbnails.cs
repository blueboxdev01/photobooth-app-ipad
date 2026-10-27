using SkiaSharp;

namespace Photobooth.Imaging;

/// <summary>
/// Small JPEG copies of the camera's photos, for screens that show a photo at a
/// fraction of its size.
///
/// <para>
/// A frame from the camera is around 8 MB and a phone on the booth's wifi draws
/// it a few hundred pixels wide. Sending the original to fill a thumbnail is most
/// of what makes a guest wait for their photos, so the page asks for these and
/// keeps the originals one tap away.
/// </para>
/// </summary>
public static class Thumbnails
{
    /// <summary>Same resampling as the strip, for the same reason: see <c>StripCompositor</c>.</summary>
    private static readonly SKSamplingOptions HighQuality =
        new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// <paramref name="sourcePath"/> as a JPEG no larger than
    /// <paramref name="longestEdge"/> on its longest side, or null when it cannot
    /// be decoded. Never scales up.
    /// </summary>
    public static byte[]? Render(string sourcePath, int longestEdge, int quality = 82)
    {
        using var decoded = StripCompositor.DecodeOne(sourcePath, longestEdge);
        if (decoded is null)
        {
            return null;
        }

        var longest = Math.Max(decoded.Width, decoded.Height);
        var factor = Math.Min(1.0, longestEdge / (double)longest);
        var width = Math.Max(1, (int)Math.Round(decoded.Width * factor));
        var height = Math.Max(1, (int)Math.Round(decoded.Height * factor));

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var surface = SKSurface.Create(info);

        using var paint = new SKPaint { IsAntialias = true };
        surface.Canvas.DrawBitmap(
            decoded,
            new SKRect(0, 0, decoded.Width, decoded.Height),
            new SKRect(0, 0, width, height),
            HighQuality,
            paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data?.ToArray();
    }

    /// <summary>
    /// Write <see cref="Render"/>'s result to <paramref name="destinationPath"/>.
    /// Returns false, leaving no file, when the photo could not be decoded.
    /// </summary>
    public static bool Write(
        string sourcePath, string destinationPath, int longestEdge, int quality = 82)
    {
        var bytes = Render(sourcePath, longestEdge, quality);
        if (bytes is null)
        {
            return false;
        }

        File.WriteAllBytes(destinationPath, bytes);
        return true;
    }
}
