using System.IO.Compression;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using Photobooth.Delivery;

namespace Photobooth.Server;

/// <summary>
/// A guest's own photos, served by the booth over its own wifi.
///
/// The replacement for a cloud link, and deliberately so: the booth's Google
/// account was suspended and took every guest's link with it. Nothing here needs
/// an account, a card, or an internet connection -- a guest joins the booth's
/// network, scans a code, and takes their photos.
///
/// <para>
/// <b>The token is the whole of the security.</b> It is the same unguessable
/// value <see cref="SessionArchive.NewToken"/> already mints per session, for
/// exactly this reason. So: no route lists sessions, an unknown token is
/// indistinguishable from a known one that has gone, and the files a link will
/// serve come from that session's own record rather than from the URL -- a name
/// in the path is never used to reach the disk.
/// </para>
/// </summary>
public static class GuestGallery
{
    /// <summary>Everything below this lives on the booth's network port.</summary>
    public const string Prefix = "/g/";

    /// <summary>
    /// Longest side of the copy of each photo the page draws. Tens of kilobytes, against
    /// the 8 MB original the tile links to.
    /// </summary>
    public const int ThumbnailEdge = 640;

    public static bool IsGalleryPath(PathString path) =>
        (path.Value ?? string.Empty).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The name of the small copy of <paramref name="photo"/>
    /// (<c>photo-2.jpg</c> becomes <c>thumb-2.jpg</c>), or null for a name that
    /// is not one of the session's photos.
    /// </summary>
    public static string? ThumbName(string photo)
    {
        const string prefix = "photo-";

        return photo.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? $"thumb-{Path.GetFileNameWithoutExtension(photo)[prefix.Length..]}.jpg"
            : null;
    }

    /// <summary>
    /// Serve a gallery request. Returns false when the path is not ours, so the
    /// caller can carry on.
    /// </summary>
    public static async Task<bool> TryServeAsync(HttpContext context, SessionArchive archive)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsGalleryPath(context.Request.Path))
        {
            return false;
        }

        // /g/{token}[/{file}]
        var rest = path[Prefix.Length..].Trim('/');
        var slash = rest.IndexOf('/');
        var token = slash < 0 ? rest : rest[..slash];
        var file = slash < 0 ? null : rest[(slash + 1)..];

        var record = archive.ByToken(token);
        if (record is null)
        {
            // The same answer for a token that never existed and one whose
            // session has been deleted. Anything else is a guessing oracle.
            await NotFoundAsync(context);
            return true;
        }

        var folder = archive.FolderFor(record);

        if (file is null)
        {
            await PageAsync(context, record, folder);
            return true;
        }

        if (file.Equals("all.zip", StringComparison.OrdinalIgnoreCase))
        {
            await ZipAsync(context, record, folder);
            return true;
        }

        await FileAsync(context, record, folder, file);
        return true;
    }

    /// <summary>
    /// One file from the session.
    ///
    /// The requested name is matched against the record's own list and the
    /// matched entry is what opens the file. A name from the URL never reaches
    /// the filesystem, so there is nothing for "../" to do.
    /// </summary>
    private static async Task FileAsync(
        HttpContext context, SessionRecord record, string folder, string requested)
    {
        var allowed = Listed(record).Concat(ThumbNames(record))
            .FirstOrDefault(name => string.Equals(name, requested, StringComparison.OrdinalIgnoreCase));

        if (allowed is null)
        {
            await NotFoundAsync(context);
            return;
        }

        var full = Path.Combine(folder, allowed);
        if (!File.Exists(full))
        {
            await NotFoundAsync(context);
            return;
        }

        // Declared from the extension rather than assumed. A GIF served as
        // image/jpeg reaches the phone as a still, and a video served as
        // anything but video/mp4 will not play: iOS trusts the type it was
        // given when saving to the camera roll.
        context.Response.ContentType = Path.GetExtension(allowed).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            _ => "image/jpeg",
        };

        // inline, deliberately, and this was the other way round.
        //
        // "attachment" makes a phone download the file, and on iOS a download
        // goes to Files -- not Photos, which is where a guest expects their
        // photos and where they can post them from. There is no web API that
        // can write to the camera roll: the share sheet needs a secure context
        // and this page is plain HTTP, because the iPad has to be able to fetch
        // the certificate over it before it trusts anything.
        //
        // So the picture is served to be looked at. The guest taps it, it fills
        // the screen, and a long press offers "Add to Photos" on iOS and
        // "Download image" on Android -- both of which land in the gallery. The
        // filename is still declared, so whichever they use keeps a name worth
        // having.
        context.Response.Headers.ContentDisposition =
            $"inline; filename=\"{Download(record, allowed)}\"";

        // A session's files are written once and never change, so a phone that
        // has them can keep them. Without this, going back to the page from a
        // photo downloads the whole lot again.
        context.Response.Headers.CacheControl = "private, max-age=86400";

        await SendFileAsync(context, full);
    }

    /// <summary>
    /// Sends the file, answering a byte-range request if it is one.
    ///
    /// iOS Safari will not play a video from a server that cannot serve ranges:
    /// it asks for the first two bytes to probe, gets the whole file back with a
    /// 200, and gives up with a black rectangle. Photos never needed this.
    /// </summary>
    private static async Task SendFileAsync(HttpContext context, string full)
    {
        var length = new FileInfo(full).Length;
        context.Response.Headers.AcceptRanges = "bytes";

        var header = context.Request.GetTypedHeaders().Range;
        var range = header is not null
            && header.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase)
            && header.Ranges.Count == 1
                ? header.Ranges.First()
                : null;

        if (range is null)
        {
            await context.Response.SendFileAsync(full);
            return;
        }

        long start;
        long end;

        if (range.From is { } first)
        {
            start = first;
            end = Math.Min(range.To ?? length - 1, length - 1);
        }
        else if (range.To is { } suffix)
        {
            // "the last N bytes"
            start = Math.Max(0, length - suffix);
            end = length - 1;
        }
        else
        {
            await context.Response.SendFileAsync(full);
            return;
        }

        if (start >= length || start > end)
        {
            context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            context.Response.Headers.ContentRange = $"bytes */{length}";
            return;
        }

        context.Response.StatusCode = StatusCodes.Status206PartialContent;
        context.Response.Headers.ContentRange = $"bytes {start}-{end}/{length}";
        context.Response.ContentLength = end - start + 1;

        await context.Response.SendFileAsync(full, start, end - start + 1);
    }

    /// <summary>
    /// The whole session as one download.
    ///
    /// A phone taking six files one at a time, each needing its own tap and
    /// permission, is the kind of thing a guest gives up on halfway through.
    /// </summary>
    private static async Task ZipAsync(HttpContext context, SessionRecord record, string folder)
    {
        context.Response.ContentType = "application/zip";
        context.Response.Headers.ContentDisposition =
            $"attachment; filename=\"photobooth-{record.FolderName}.zip\"";

        // ZipArchive writes the central directory from Dispose, synchronously,
        // and has no async disposal to offer instead. Kestrel refuses
        // synchronous writes by default, so without this the entries stream out
        // fine, the footer throws, and the guest is handed a 200 and a corrupt
        // archive -- a failure that looks like success from every angle.
        var sync = context.Features.Get<IHttpBodyControlFeature>();
        if (sync is not null)
        {
            sync.AllowSynchronousIO = true;
        }

        // Streamed rather than built in memory: a session is around 25 MB of
        // JPEGs and there is no reason to hold all of it twice.
        using var zip = new ZipArchive(context.Response.Body, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var name in Listed(record))
        {
            var full = Path.Combine(folder, name);
            if (!File.Exists(full))
            {
                continue;
            }

            var entry = zip.CreateEntry(Download(record, name), CompressionLevel.NoCompression);
            await using var source = File.OpenRead(full);
            await using var target = entry.Open();
            await source.CopyToAsync(target);
        }
    }

    /// <summary>
    /// What a session is willing to hand over: the strip, its animation, and the
    /// photos.
    ///
    /// Not session.json -- it carries the token, and a guest who forwards their
    /// zip should not be forwarding the key to their own gallery. Not the QR
    /// either; it is a picture of the link they already followed.
    /// </summary>
    private static IEnumerable<string> Listed(SessionRecord record)
    {
        yield return record.Strip;

        // Null for a session taken before animations existed, and for any where
        // building one failed. Guarded rather than yielded blindly, because a
        // null here would reach the allowlist, the page and the zip loop alike.
        if (record.Animation is { } animation)
        {
            yield return animation;
        }

        foreach (var photo in record.Photos)
        {
            yield return photo;
        }
    }

    /// <summary>
    /// The small copies the page may show. Served on request but deliberately
    /// not in <see cref="Listed"/>: the zip and the tile list are built from
    /// that, and a guest should not be handed two copies of every photo.
    /// </summary>
    private static IEnumerable<string> ThumbNames(SessionRecord record) =>
        record.Photos.Select(ThumbName).OfType<string>();

    /// <summary>
    /// A name worth having in a camera roll. "photo-1.jpg" from three different
    /// booths collides; the session's own name does not.
    /// </summary>
    private static string Download(SessionRecord record, string name) =>
        $"{record.FolderName}-{name}";

    private static async Task NotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/html; charset=utf-8";

        await context.Response.WriteAsync("""
            <!doctype html>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Photos not found</title>
            <style>
              body { font: 17px/1.5 -apple-system, system-ui, sans-serif;
                     margin: 0 auto; padding: 40px 24px; max-width: 30rem;
                     color: #14161a; text-align: center; }
            </style>
            <h1>We cannot find those photos</h1>
            <p>The link may be mistyped, or these photos may have been cleared.
               Ask at the booth and we will sort it out.</p>
            """);
    }

    /// <summary>
    /// The guest's page.
    ///
    /// Hand-written and entirely self-contained -- no script, no font, no
    /// stylesheet from anywhere. It is opened on a phone that has just joined a
    /// network with no route to the internet, so anything it tried to fetch from
    /// outside would simply hang.
    /// </summary>
    private static async Task PageAsync(HttpContext context, SessionRecord record, string folder)
    {
        var token = WebUtility.HtmlEncode(record.Token);
        var taken = record.CreatedUtc.ToLocalTime().ToString("d MMMM, HH:mm");

        var tiles = string.Join("\n", Listed(record).Select(name =>
        {
            var href = $"{Prefix}{Uri.EscapeDataString(record.Token)}/{Uri.EscapeDataString(name)}";
            var isStrip = string.Equals(name, record.Strip, StringComparison.OrdinalIgnoreCase);
            var isAnimation = string.Equals(name, record.Animation, StringComparison.OrdinalIgnoreCase);

            // Named rather than left to the fallback, which would show a guest
            // the raw filename.
            var label = (isStrip, isAnimation) switch
            {
                (true, _) => "The strip",
                (_, true) => "The video",
                _ => name.Replace("photo-", "Photo ").Replace(".jpg", ""),
            };

            // Both the strip and the animation span the grid: one is tall and one
            // is worth watching, and neither reads at thumbnail size.
            var wide = isStrip || isAnimation ? " tile--strip" : string.Empty;

            // A video is shown playing, without controls, so a tap lands on the
            // link and opens the file itself. That is deliberate: the full-screen
            // player is the one with iOS's share button, and Save Video there is
            // what puts it in Photos, from where it can be posted.
            //
            // Nothing here pulls more than it has to. The video fetches its
            // metadata and shows the strip until it plays, rather than racing the
            // photos for the same wifi. A photo tile draws its small copy and
            // loads it as it scrolls into view; the tile still links to the
            // original, so the camera roll gets the full-size file. A session
            // taken before the small copies existed has none, and falls back to
            // the original.
            var thumb = ThumbName(name);
            var thumbHref = thumb is not null && File.Exists(Path.Combine(folder, thumb))
                ? $"{Prefix}{Uri.EscapeDataString(record.Token)}/{Uri.EscapeDataString(thumb)}"
                : href;
            var stripHref = $"{Prefix}{Uri.EscapeDataString(record.Token)}/{Uri.EscapeDataString(record.Strip)}";

            var media = name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? $"""<video src="{href}" poster="{stripHref}" autoplay loop muted playsinline preload="metadata"></video>"""
                : $"""<img src="{thumbHref}" alt="" loading="lazy" decoding="async">""";

            return $"""
                      <a class="tile{wide}" href="{href}">
                        {media}
                        <span>{WebUtility.HtmlEncode(label)}</span>
                      </a>
                """;
        }));

        context.Response.ContentType = "text/html; charset=utf-8";

        await context.Response.WriteAsync($$"""
            <!doctype html>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Your photos</title>
            <style>
              :root { color-scheme: light dark; }
              body { font: 17px/1.5 -apple-system, system-ui, sans-serif;
                     margin: 0 auto; padding: 24px 16px 48px; max-width: 46rem; }
              h1 { font-size: 26px; margin: 0 0 4px; }
              .when { color: #6b7280; margin: 0 0 22px; font-size: 14px; }
              .grid { display: grid; gap: 14px;
                      grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); }
              .tile { display: block; text-decoration: none; color: inherit;
                      border: 1px solid #d7dae0; border-radius: 12px; overflow: hidden;
                      background: #fff; }
              .tile--strip { grid-column: 1 / -1; }
              .tile img, .tile video { display: block; width: 100%; height: auto; }
              .tile--strip img, .tile--strip video { max-height: 60vh; width: auto; max-width: 100%; margin: 0 auto; }
              .tile span { display: block; padding: 9px 12px; font-size: 13.5px;
                           font-weight: 600; }
              .how { margin: 0 0 18px; padding: 12px 14px; font-size: 14.5px;
                     line-height: 1.5; background: #eef4ff; border-radius: 10px; }
              .all { display: block; margin: 22px 0 0; padding: 13px;
                     background: #eef0f3; color: #3c4450; text-align: center;
                     border-radius: 12px; text-decoration: none; font-weight: 600;
                     font-size: 14px; }
              .hint { color: #6b7280; font-size: 13.5px; margin-top: 18px; }
              @media (prefers-color-scheme: dark) {
                body { background: #14161a; color: #e8eaed; }
                .tile { background: #1c1f24; border-color: #2b2f36; }
                .when, .hint { color: #9aa1ab; }
                .how { background: #18202c; }
                .all { background: #1c1f24; color: #c7cdd6; }
              }
            </style>

            <h1>Your photos</h1>
            <p class="when">Taken {{taken}}</p>

            <p class="how"><b>Tap a photo</b>, then <b>touch and hold it</b> and choose
               <b>Add to Photos</b> &mdash; or <b>Download image</b> on Android. That
               puts it straight in your camera roll.</p>
            <p class="how"><b>To post the video</b> to Facebook or Instagram, tap it,
               then use the <b>share</b> button and choose <b>Save Video</b>.</p>

            <div class="grid">
            {{tiles}}
            </div>

            <a class="all" href="{{Prefix}}{{token}}/all.zip" download>Or download everything as one file</a>

            <p class="hint">This page works only while you are on the booth&rsquo;s
               wifi &mdash; save what you want before you leave.</p>
            """);
    }
}
