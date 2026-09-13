using Microsoft.AspNetCore.Http;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// What the network-facing port will and will not answer.
///
/// This is the boundary that keeps a venue's wifi out of a running booth. The
/// failure it exists to prevent is quiet: everything looks fine, and a stranger
/// on the guest network can abort a session or pull a diagnostics bundle.
/// </summary>
public sealed class GuestEndpointTests
{
    /// <summary>Everything the guest display needs in order to work at all.</summary>
    [Theory]
    [InlineData("/display")]
    [InlineData("/hub/session")]
    [InlineData("/api/state")]
    [InlineData("/api/delivery")]
    [InlineData("/api/photos/IMG_0001.JPG")]
    [InlineData("/api/sessions/2026-09-13_1042_ab12cd/qr.png")]
    [InlineData("/api/settings/display-background")]
    [InlineData("/assets/index-abc123.js")]
    [InlineData("/assets/index-abc123.css")]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/favicon.ico")]
    public void The_guest_display_is_served(string path)
    {
        Assert.True(GuestEndpoint.IsGuestPath(path), $"{path} must reach the iPad");
    }

    /// <summary>
    /// The console and everything that can change or end a session. A stranger
    /// reaching any of these is the reason the allowlist exists.
    /// </summary>
    [Theory]
    [InlineData("/operator")]
    [InlineData("/diagnostics")]
    [InlineData("/templates")]
    [InlineData("/api/settings")]
    [InlineData("/api/settings/check-folder")]
    [InlineData("/api/session/arm")]
    [InlineData("/api/session/abort")]
    [InlineData("/api/session/accept")]
    [InlineData("/api/session/retake/2")]
    [InlineData("/api/session/order")]
    [InlineData("/api/mock/press")]
    [InlineData("/api/diagnostics")]
    [InlineData("/api/diagnostics/bundle")]
    [InlineData("/api/templates")]
    [InlineData("/api/sessions")]
    [InlineData("/api/delivery/authorize")]
    [InlineData("/api/delivery/sign-out")]
    public void Everything_else_is_refused(string path)
    {
        Assert.False(GuestEndpoint.IsGuestPath(path), $"{path} must NOT be reachable from the network");
    }

    /// <summary>
    /// A prefix match that is too loose is how allowlists leak. /api/sessions is
    /// the whole archive; only a single session's QR belongs on the iPad.
    /// </summary>
    [Fact]
    public void The_session_archive_is_not_opened_up_by_the_qr_rule()
    {
        Assert.True(GuestEndpoint.IsGuestPath("/api/sessions/2026-09-13_1042_ab12cd/qr.png"));

        Assert.False(GuestEndpoint.IsGuestPath("/api/sessions"));
        Assert.False(GuestEndpoint.IsGuestPath("/api/sessions/2026-09-13_1042_ab12cd/photo-1.jpg"));
        Assert.False(GuestEndpoint.IsGuestPath("/api/sessions/2026-09-13_1042_ab12cd/session.json"));
        Assert.False(GuestEndpoint.IsGuestPath("/api/sessions/2026-09-13_1042_ab12cd/strip.jpg"));
    }

    /// <summary>
    /// Likewise the one settings route the display legitimately needs must not
    /// drag the rest of settings in behind it.
    /// </summary>
    [Fact]
    public void The_backdrop_route_does_not_open_the_rest_of_settings()
    {
        Assert.True(GuestEndpoint.IsGuestPath("/api/settings/display-background"));

        Assert.False(GuestEndpoint.IsGuestPath("/api/settings"));
        Assert.False(GuestEndpoint.IsGuestPath("/api/settings/check-folder"));
    }

    /// <summary>Case is not a way round it.</summary>
    [Theory]
    [InlineData("/OPERATOR")]
    [InlineData("/Api/Session/Abort")]
    [InlineData("/API/DIAGNOSTICS/BUNDLE")]
    public void Casing_does_not_get_past_it(string path)
    {
        Assert.False(GuestEndpoint.IsGuestPath(path));
    }

    [Fact]
    public void The_display_page_itself_is_matched_exactly()
    {
        Assert.True(GuestEndpoint.IsGuestPath("/display"));
        Assert.True(GuestEndpoint.IsGuestPath("/DISPLAY"));

        // Not a prefix: /display-something-else is not the guest display.
        Assert.False(GuestEndpoint.IsGuestPath("/displayed"));
        Assert.False(GuestEndpoint.IsGuestPath("/display/../operator"));
    }
}
