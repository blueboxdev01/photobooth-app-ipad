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

    public static bool IsGalleryPath(PathString path) =>
        (path.Value ?? string.Empty).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

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
            await PageAsync(context, record);
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
        var allowed = Listed(record)
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

        context.Response.ContentType = allowed.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";

        // attachment, so a phone saves it rather than showing it in a tab the
        // guest then has to long-press.
        context.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{Download(record, allowed)}\"";

        await context.Response.SendFileAsync(full);
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
    /// What a session is willing to hand over: the strip and the photos.
    ///
    /// Not session.json -- it carries the token, and a guest who forwards their
    /// zip should not be forwarding the key to their own gallery. Not the QR
    /// either; it is a picture of the link they already followed.
    /// </summary>
    private static IEnumerable<string> Listed(SessionRecord record)
    {
        yield return record.Strip;

        foreach (var photo in record.Photos)
        {
            yield return photo;
        }
    }

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
    private static async Task PageAsync(HttpContext context, SessionRecord record)
    {
        var token = WebUtility.HtmlEncode(record.Token);
        var taken = record.CreatedUtc.ToLocalTime().ToString("d MMMM, HH:mm");

        var tiles = string.Join("\n", Listed(record).Select(name =>
        {
            var href = $"{Prefix}{Uri.EscapeDataString(record.Token)}/{Uri.EscapeDataString(name)}";
            var isStrip = string.Equals(name, record.Strip, StringComparison.OrdinalIgnoreCase);
            var label = isStrip ? "The strip" : name.Replace("photo-", "Photo ").Replace(".jpg", "");

            return $"""
                      <a class="tile{(isStrip ? " tile--strip" : "")}" href="{href}" download>
                        <img src="{href}" alt="">
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
              .tile img { display: block; width: 100%; height: auto; }
              .tile--strip img { max-height: 60vh; width: auto; margin: 0 auto; }
              .tile span { display: block; padding: 9px 12px; font-size: 13.5px;
                           font-weight: 600; }
              .all { display: block; margin: 22px 0 0; padding: 15px;
                     background: #2563eb; color: #fff; text-align: center;
                     border-radius: 12px; text-decoration: none; font-weight: 700; }
              .hint { color: #6b7280; font-size: 13.5px; margin-top: 18px; }
              @media (prefers-color-scheme: dark) {
                body { background: #14161a; color: #e8eaed; }
                .tile { background: #1c1f24; border-color: #2b2f36; }
                .when, .hint { color: #9aa1ab; }
              }
            </style>

            <h1>Your photos</h1>
            <p class="when">Taken {{taken}}</p>

            <a class="all" href="{{Prefix}}{{token}}/all.zip" download>Download all as a zip</a>

            <div class="grid">
            {{tiles}}
            </div>

            <p class="hint">Tap a photo to save it on its own. This page works only
               while you are on the booth&rsquo;s wifi &mdash; save what you want
               before you leave.</p>
            """);
    }
}
