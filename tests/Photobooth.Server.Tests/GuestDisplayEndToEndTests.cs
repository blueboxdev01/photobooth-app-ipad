using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace Photobooth.Server.Tests;

/// <summary>
/// What the guest screen actually asks a running booth for.
///
/// <para>
/// This exists because three faults in a row were the same shape: the port
/// allowlist not listing something the display cannot work without. SignalR's
/// handshake, then the finished strip. Every unit test passed through both,
/// because a unit test can only check the URLs somebody remembered to write
/// down -- and forgetting one is precisely the bug.
/// </para>
///
/// <para>
/// So these do not assert against a list. They start a real booth, drive a real
/// session, and then <b>behave like the iPad</b>: open a genuine SignalR
/// connection, fetch what the guest screen displays, and check the console is
/// still shut. A missing entry fails here without anyone having predicted which
/// entry it would be.
/// </para>
/// </summary>
/// <remarks>
/// One booth for the whole class, not one per test. xUnit builds a fresh
/// instance of a test class for every method, so an IAsyncLifetime here would
/// start and stop a real server a dozen times over.
/// </remarks>
public sealed class GuestDisplayEndToEndTests : IClassFixture<BoothProcess>
{
    private readonly BoothProcess _booth;

    public GuestDisplayEndToEndTests(BoothProcess booth) => _booth = booth;

    /// <summary>Shoot a session on the mock camera and accept it.</summary>
    private async Task<string> RunSessionAsync(int shots = 3)
    {
        (await _booth.Operator.PostAsync("/api/session/arm", null)).EnsureSuccessStatusCode();

        for (var i = 0; i < shots; i++)
        {
            (await _booth.Operator.PostAsync("/api/mock/press?mode=Normal", null))
                .EnsureSuccessStatusCode();
        }

        await WaitForStateAsync("ReviewShots");

        (await _booth.Operator.PostAsync("/api/session/accept", null)).EnsureSuccessStatusCode();

        await WaitForStateAsync("Done");

        var state = await _booth.Operator.GetFromJsonAsync<StateResponse>("/api/state");
        var folder = state?.Session?.SessionFolder;

        Assert.False(string.IsNullOrWhiteSpace(folder), "the session produced no archive folder");
        return folder!;
    }

    private async Task WaitForStateAsync(string wanted)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        string? seen = null;

        while (DateTime.UtcNow < deadline)
        {
            var state = await _booth.Operator.GetFromJsonAsync<StateResponse>("/api/state");
            seen = state?.Session?.State;

            if (seen == wanted)
            {
                return;
            }

            await Task.Delay(200);
        }

        Assert.Fail($"the session never reached {wanted}; it was {seen}");
    }

    private sealed record StateResponse(SessionPart? Session);
    private sealed record SessionPart(string? State, string? SessionFolder);

    // --- the live connection --------------------------------------------------

    /// <summary>
    /// The one that would have caught the handshake. SignalR does not open a
    /// socket at the hub address -- it POSTs to <c>{hub}/negotiate</c> first, and
    /// allowing the hub without its handshake gave the iPad a page that loaded
    /// and then sat on "Connecting..." for ever.
    ///
    /// <para>
    /// Uses a real SignalR client against the real display port, so nothing here
    /// depends on anyone knowing that <c>/negotiate</c> exists.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_display_can_open_its_live_connection()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl($"https://localhost:{_booth.DisplayPort}/hub/session", options =>
            {
                // The handshake goes through here...
                options.HttpMessageHandlerFactory = _ => BoothProcess.Insecure();

                // ...but the socket that follows does not. Without this the
                // negotiate succeeds and the upgrade fails on the booth's own
                // certificate, which would look exactly like the bug this test
                // is here to catch.
                options.WebSocketConfiguration = socket =>
                    socket.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            })
            .Build();

        var state = new TaskCompletionSource();
        connection.On<object>("state", _ => state.TrySetResult());

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);

        // The booth pushes a snapshot on connect; without it the screen renders
        // nothing however healthy the socket is.
        var pushed = await Task.WhenAny(state.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.True(pushed == state.Task, "the booth never pushed a state snapshot");
    }

    // --- what the screen shows ------------------------------------------------

    /// <summary>
    /// The one that would have caught the white box. After a session the display
    /// shows the strip, and it fetches it from the archive route -- where only
    /// the QR used to be allowed, so the strip 404'd and the screen read
    /// "All done" over an empty frame.
    /// </summary>
    [Fact]
    public async Task The_display_can_fetch_what_it_shows_after_a_session()
    {
        var folder = await RunSessionAsync();

        var strip = await _booth.Display.GetAsync($"/api/sessions/{folder}/strip.jpg");
        Assert.Equal(HttpStatusCode.OK, strip.StatusCode);
        Assert.Equal("image/jpeg", strip.Content.Headers.ContentType?.MediaType);

        var animation = await _booth.Display.GetAsync($"/api/sessions/{folder}/strip.gif");
        Assert.Equal(HttpStatusCode.OK, animation.StatusCode);
        Assert.Equal("image/gif", animation.Content.Headers.ContentType?.MediaType);

        // Not an empty body pretending to be a picture.
        Assert.True(
            (await animation.Content.ReadAsByteArrayAsync()).Length > 1000,
            "the animation came back suspiciously small");
    }

    /// <summary>Everything the guest screen loads on its way up.</summary>
    [Theory]
    [InlineData("/display")]
    [InlineData("/api/state")]
    [InlineData("/api/delivery")]
    public async Task The_display_can_fetch_what_it_starts_with(string path)
    {
        var response = await _booth.Display.GetAsync(path);

        Assert.True(response.IsSuccessStatusCode,
            $"{path} returned {(int)response.StatusCode}; the guest screen needs it");
    }

    // --- and nothing else -----------------------------------------------------

    /// <summary>
    /// The boundary this allowlist exists for. Widening it to fix the display
    /// must not have opened the console, the settings, or the record that
    /// carries a guest's private link.
    /// </summary>
    [Theory]
    [InlineData("/operator")]
    [InlineData("/diagnostics")]
    [InlineData("/api/settings")]
    [InlineData("/api/diagnostics/bundle")]
    [InlineData("/api/sessions")]
    public async Task The_console_is_still_shut_on_the_display_port(string path)
    {
        var response = await _booth.Display.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The record carries the token, and the token is the whole of the gallery's
    /// security -- so neither network port may hand it over.
    ///
    /// <para>
    /// Asserted on the <b>content</b>, not the status code, and this test had to
    /// learn that the hard way: it first checked for a 404 and failed against a
    /// 200. The certificate port answers any unrecognised path with the iPad
    /// setup page, so it returns 200 while serving nothing of the sort. A status
    /// code alone would equally have "passed" had the record genuinely been
    /// served, which makes it the wrong question entirely. What matters is
    /// whether the token comes back.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_sessions_record_is_never_served_to_the_network()
    {
        var folder = await RunSessionAsync();

        // Taken from the console, which is allowed to see it, so the test knows
        // the real secret rather than a guess at its shape.
        var record = await _booth.Operator.GetStringAsync($"/api/sessions/{folder}/session.json");
        var token = System.Text.Json.JsonDocument.Parse(record)
            .RootElement.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token), "the session had no token to protect");

        foreach (var client in new[] { _booth.Display, _booth.Guest })
        {
            var response = await client.GetAsync($"/api/sessions/{folder}/session.json");
            var body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain(token!, body, StringComparison.Ordinal);
            Assert.DoesNotContain("sourceFiles", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Checked by effect rather than by status code. <c>/operator</c> on the
    /// certificate port returns a page -- the iPad setup instructions -- so a
    /// status code alone once "proved" a boundary that was not there.
    /// </summary>
    [Fact]
    public async Task A_stranger_on_the_network_cannot_end_a_session()
    {
        await RunSessionAsync();

        await _booth.Display.PostAsync("/api/session/abort", null);
        await _booth.Guest.PostAsync("/api/session/abort", null);

        var state = await _booth.Operator.GetFromJsonAsync<StateResponse>("/api/state");

        Assert.Equal("Done", state?.Session?.State);
    }
}
