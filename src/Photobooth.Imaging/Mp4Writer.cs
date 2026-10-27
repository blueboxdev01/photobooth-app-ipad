using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Photobooth.Imaging;

/// <summary>
/// Writes rendered frames out as one H.264 MP4, by handing them to ffmpeg.
///
/// <para>
/// MP4 rather than GIF, because a guest wants to <i>post</i> this. Instagram and
/// TikTok accept no GIF at all and Facebook's phone picker turns one into a
/// still, while every one of them takes a short H.264 clip. Neither SkiaSharp
/// nor ImageSharp can write H.264, and a managed encoder is not something to
/// hand-roll for a three-second clip, so ffmpeg does the encoding.
/// </para>
///
/// <para>
/// The output is deliberately the most boring video there is, because that is
/// what every upload form accepts: H.264, <c>yuv420p</c>, even dimensions, no
/// audio, and the index at the front so a phone can start playing before it has
/// finished downloading. <c>yuv420p</c> and even sides are not optional --
/// without them the file plays on a laptop and is refused by a phone.
/// </para>
/// </summary>
internal static class Mp4Writer
{
    /// <summary>
    /// Encoders to try, best first. <c>libx264</c> is in the "gpl" ffmpeg
    /// builds; the "lgpl" builds that are fine to redistribute have only
    /// <c>h264_mf</c>, which drives the H.264 encoder built into Windows. The
    /// release bundles an lgpl build, so the second one is the one that runs on
    /// the booth.
    /// </summary>
    private static readonly (string Name, string[] Args)[] Encoders =
    [
        ("libx264", ["-preset", "veryfast", "-crf", "21", "-profile:v", "high"]),

        // 2 Mbit/s is ample for a 333x1000 clip that is three photos held for
        // 400 ms each, and at the 6 s the clip runs for it is about 1.5 MB. The
        // 4M this used to ask for roughly doubled the download for no visible
        // difference. Check a real session's strip.mp4 before lowering it again.
        ("h264_mf", ["-b:v", "2M"]),
    ];

    /// <summary>
    /// A hung encoder must not hold a guest at "preparing your photos". The real
    /// encode is well under two seconds.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Constant output rate, so every player agrees on the timing.</summary>
    private const int OutputFps = 25;

    /// <summary>
    /// Encode one pass of <paramref name="frames"/>, repeated until the clip is
    /// at least <see cref="AnimationOptions.MinimumSeconds"/> long.
    /// </summary>
    /// <param name="frames">
    /// Frames in <see cref="SKColorType.Rgba8888"/>, all the same size.
    /// </param>
    public static void Write(
        IReadOnlyList<SKBitmap> frames, string outputPath, AnimationOptions options)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("An animation needs at least one frame.", nameof(frames));
        }

        var ffmpeg = FindFfmpeg(options.FfmpegPath);

        var work = Path.Combine(Path.GetTempPath(), $"pb-mp4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);

        try
        {
            for (var i = 0; i < frames.Count; i++)
            {
                using var image = SKImage.FromBitmap(frames[i]);
                using var png = image.Encode(SKEncodedImageFormat.Png, 100)
                    ?? throw new InvalidOperationException($"Could not encode frame {i + 1}.");
                using var file = File.Create(Path.Combine(work, FrameName(i)));
                png.SaveTo(file);
            }

            File.WriteAllText(
                Path.Combine(work, "frames.txt"),
                ConcatList(frames.Count, options),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string? lastError = null;

            foreach (var (name, extra) in Encoders)
            {
                // Never leave half a file behind from an encoder that failed.
                TryDelete(outputPath);

                var (code, error) = Run(ffmpeg, work, Arguments(name, extra, outputPath));
                if (code == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    return;
                }

                lastError = $"{name}: {error}";
            }

            TryDelete(outputPath);
            throw new InvalidOperationException(
                $"ffmpeg could not encode the video. {lastError}");
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { /* best effort */ }
        }
    }

    private static string FrameName(int index) => $"frame-{index}.png";

    /// <summary>
    /// ffmpeg's concat list: each frame held for the delay, the whole cycle
    /// repeated to fill the minimum length.
    ///
    /// The last file is listed once more with no duration. The concat demuxer
    /// ignores the duration of the final entry, so without that line the last
    /// photo flashes by for a single frame.
    /// </summary>
    internal static string ConcatList(int frameCount, AnimationOptions options)
    {
        // Whole milliseconds, so the repeat count is not at the mercy of
        // 3 x 0.4 not quite being 1.2.
        var holdMs = Math.Max(40, options.FrameDelayMilliseconds);
        var cycleMs = frameCount * holdMs;
        var repeats = Math.Max(1, (int)Math.Ceiling(options.MinimumSeconds * 1000.0 / cycleMs));
        var hold = holdMs / 1000.0;

        var list = new StringBuilder();

        for (var r = 0; r < repeats; r++)
        {
            for (var i = 0; i < frameCount; i++)
            {
                list.Append("file '").Append(FrameName(i)).Append("'\n");
                list.Append("duration ")
                    .Append(hold.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append('\n');
            }
        }

        list.Append("file '").Append(FrameName(frameCount - 1)).Append("'\n");
        return list.ToString();
    }

    private static IReadOnlyList<string> Arguments(
        string encoder, string[] encoderArguments, string outputPath)
    {
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "concat", "-safe", "0", "-i", "frames.txt",

            // Constant frame rate, sides rounded down to even (H.264 cannot
            // express an odd one) and the pixel format every phone decodes.
            "-vf", $"fps={OutputFps},scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p",
            "-c:v", encoder,
        };

        args.AddRange(encoderArguments);

        // moov atom first, so playback starts before the download finishes.
        args.AddRange(["-an", "-movflags", "+faststart", outputPath]);
        return args;
    }

    private static (int Code, string Error) Run(
        string ffmpeg, string workingDirectory, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(ffmpeg)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {ffmpeg}.");

        // Both streams drained concurrently. Reading one to the end while the
        // other fills its pipe buffer is the classic way to deadlock a child.
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(Timeout))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException($"ffmpeg did not finish within {Timeout.TotalSeconds:0} seconds.");
        }

        // The no-argument overload waits for the redirected streams to reach EOF.
        process.WaitForExit();
        _ = output.Result;

        return (process.ExitCode, error.Result.Trim());
    }

    /// <summary>
    /// Where ffmpeg is: the path given, then <c>ffmpeg\ffmpeg.exe</c> or
    /// <c>ffmpeg.exe</c> beside the app (how a release ships it), then whatever
    /// the PATH finds -- which is how a developer's own install gets used.
    /// </summary>
    internal static string FindFfmpeg(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        foreach (var candidate in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", exe),
            Path.Combine(AppContext.BaseDirectory, exe),
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return exe;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
