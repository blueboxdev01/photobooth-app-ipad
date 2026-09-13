using Photobooth.Delivery;

namespace Photobooth.Server;

public sealed class GuestDisplayOptions
{
    public const string SectionName = "GuestDisplay";

    /// <summary>
    /// Whether the booth is reachable from the network at all.
    ///
    /// <b>Off by default.</b> Binding to the network is a decision someone makes
    /// for a specific booth, not something a build should start doing because it
    /// was unzipped.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Plain HTTP, serving the root certificate and nothing else.
    ///
    /// It has to be plain: the iPad cannot fetch the root over the HTTPS that the
    /// root is what makes trustworthy.
    /// </summary>
    public int CertPort { get; set; } = 5001;

    /// <summary>HTTPS, serving the guest display.</summary>
    public int DisplayPort { get; set; } = 5002;
}

/// <summary>
/// Which requests each listening port will answer.
///
/// Binding the app to the network exposes <i>everything</i> -- the operator
/// console, the diagnostics bundle, and the endpoints that abort a session. On a
/// venue's wifi that is a stranger's reach into a running booth, so the network
/// ports serve the guest display and refuse the rest.
///
/// Judged on the port the connection arrived on rather than on the requested
/// host, because a host header is whatever the caller typed and a local port is
/// not.
/// </summary>
public static class GuestEndpoint
{
    /// <summary>
    /// What the guest display needs to run: its own page, the bundle it is built
    /// from, the state feed, the photos it shows and the QR it ends on.
    /// </summary>
    private static readonly string[] GuestPaths =
    [
        "/display",
        "/hub/session",
        "/api/state",
        "/api/delivery",
        "/api/photos/",
        "/api/settings/display-background",
    ];

    /// <summary>
    /// Whether a path is part of the guest display. Internal so it can be tested
    /// directly -- this list is the security boundary, and "does ASP.NET route by
    /// port" is not the part worth doubting.
    /// </summary>
    internal static bool IsGuestPath(PathString path)
    {
        var value = path.Value ?? "/";

        // Static assets: the display is a single bundle, and refusing its own
        // JavaScript would be a blank screen rather than a blocked page.
        if (value.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
            value is "/" or "/favicon.ico" or "/index.html")
        {
            return true;
        }

        // A finished session's QR, which is the last thing the display shows.
        if (value.StartsWith("/api/sessions/", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith("/qr.png", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GuestPaths.Any(allowed =>
            allowed.EndsWith('/')
                ? value.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
                : value.Equals(allowed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Refuse anything but the guest display on the network-facing ports, and
    /// serve the root certificate on the plain-HTTP one.
    /// </summary>
    public static IApplicationBuilder UseGuestEndpointRules(
        this IApplicationBuilder app, GuestDisplayOptions options, BoothCertificates certificates)
    {
        return app.Use(async (context, next) =>
        {
            var port = context.Connection.LocalPort;

            if (port == options.CertPort)
            {
                // A guest's own photos, which is the other thing this plain-HTTP
                // port is for. Downloads need no secure context, so guests are
                // spared the certificate the iPad needs.
                var archive = context.RequestServices.GetRequiredService<SessionArchive>();
                if (await GuestGallery.TryServeAsync(context, archive))
                {
                    return;
                }

                await ServeCertificateSideAsync(context, options, certificates);
                return;
            }

            // The gallery belongs to guests on the booth's wifi, not to the
            // iPad, and it is never served from the operator's own port.
            if (GuestGallery.IsGalleryPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (port == options.DisplayPort && !IsGuestPath(context.Request.Path))
            {
                // 404 rather than 403: there is nothing useful to say to someone
                // probing a booth, and the operator console is reached from the
                // machine itself anyway.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });
    }

    /// <summary>
    /// The plain-HTTP port. It exists for one job -- handing an iPad the root
    /// certificate before the iPad trusts anything -- and does nothing else.
    /// </summary>
    private static async Task ServeCertificateSideAsync(
        HttpContext context, GuestDisplayOptions options, BoothCertificates certificates)
    {
        if (context.Request.Path.Equals("/cert", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.Equals("/cert.crt", StringComparison.OrdinalIgnoreCase))
        {
            // application/x-x509-ca-cert is what makes iOS offer to install it as
            // a profile rather than downloading it as a file nobody can open.
            context.Response.ContentType = "application/x-x509-ca-cert";
            context.Response.Headers.ContentDisposition = "attachment; filename=\"photobooth-ca.crt\"";
            await context.Response.Body.WriteAsync(certificates.AuthorityDer);
            return;
        }

        var display =
            $"https://{BoothCertificates.PreferredHost()}:{options.DisplayPort}/display";

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";

        // Deliberately hand-written and dependency-free: this page is read on an
        // iPad that does not yet trust the booth, so it cannot load anything the
        // booth would normally serve over HTTPS.
        // $$ so the CSS keeps its own braces: with a single $ every brace in a
        // stylesheet would have to be doubled, which is unreadable.
        await context.Response.WriteAsync($$"""
            <!doctype html>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Photobooth guest display</title>
            <style>
              body { font: 17px/1.5 -apple-system, system-ui, sans-serif;
                     margin: 0 auto; padding: 32px 24px; max-width: 34rem; color: #14161a; }
              h1 { font-size: 22px; }
              ol { padding-left: 1.2em; }
              li { margin-bottom: 14px; }
              a.btn { display: inline-block; background: #2563eb; color: #fff;
                      padding: 12px 18px; border-radius: 10px; text-decoration: none;
                      font-weight: 600; }
              code { background: #eef0f3; padding: 2px 6px; border-radius: 5px;
                     font-size: 15px; word-break: break-all; }
              small { color: #5b6270; }
            </style>
            <h1>Set this iPad up as the guest display</h1>
            <ol>
              <li><a class="btn" href="/cert">Install the booth certificate</a></li>
              <li>Open <b>Settings</b> and install the downloaded profile.</li>
              <li>Then <b>Settings &rsaquo; General &rsaquo; About &rsaquo; Certificate
                  Trust Settings</b> and switch this booth <b>on</b>.
                  <br><small>This second step is the one people miss. Without it the
                  display will not load.</small></li>
              <li>Open <code>{{display}}</code></li>
            </ol>
            """);
    }
}
