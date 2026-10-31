using System.Net;
using System.Net.Http.Json;

namespace Photobooth.Server.Tests;

/// <summary>
/// The guest display over plain HTTP, the way an iPad with no certificate opens
/// it, and what that port must still refuse now that it serves the display.
/// </summary>
public sealed class PlainDisplayEndToEndTests : IClassFixture<BoothProcess>
{
    private readonly BoothProcess _booth;

    public PlainDisplayEndToEndTests(BoothProcess booth) => _booth = booth;

    [Theory]
    [InlineData("/display")]
    [InlineData("/api/state")]
    [InlineData("/api/delivery")]
    public async Task The_display_loads_over_plain_http(string path)
    {
        var response = await _booth.Guest.GetAsync(path);

        Assert.True(response.IsSuccessStatusCode,
            $"{path} returned {(int)response.StatusCode} on the plain port");
        Assert.DoesNotContain("Install the booth certificate",
            await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_bare_address_goes_to_the_display()
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = _booth.Guest.BaseAddress,
        };

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/display", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task The_certificate_is_still_offered_for_an_older_ipad()
    {
        var response = await _booth.Guest.GetAsync("/cert");

        Assert.Equal("application/x-x509-ca-cert", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// The watch folder holds every guest's raw shots under guessable names. From
    /// the wifi, only the ones on screen now may be fetched.
    /// </summary>
    [Fact]
    public async Task A_raw_photo_from_another_session_is_not_served_to_the_wifi()
    {
        Directory.CreateDirectory(_booth.WatchFolder);
        await File.WriteAllBytesAsync(
            Path.Combine(_booth.WatchFolder, "IMG_7001.JPG"), new byte[2048]);

        Assert.Equal(HttpStatusCode.OK,
            (await _booth.Operator.GetAsync("/api/photos/IMG_7001.JPG")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _booth.Guest.GetAsync("/api/photos/IMG_7001.JPG")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _booth.Display.GetAsync("/api/photos/IMG_7001.JPG")).StatusCode);
    }

    /// <summary>
    /// The claiming-station list carries tokens. A booth not using that station
    /// has nothing to put in it.
    /// </summary>
    [Fact]
    public async Task The_claim_list_is_empty_when_there_is_no_claiming_station()
    {
        var board = await _booth.Guest.GetFromJsonAsync<ClaimResponse>("/api/claim");

        Assert.NotNull(board);
        Assert.Empty(board!.Guests);
    }

    [Theory]
    [InlineData("/operator")]
    [InlineData("/pickup")]
    [InlineData("/api/pickup")]
    [InlineData("/api/pickup/screen")]
    [InlineData("/api/settings")]
    public async Task The_console_is_still_shut_on_the_plain_port(string path)
    {
        var body = await (await _booth.Guest.GetAsync(path)).Content.ReadAsStringAsync();

        // The plain port answers anything it does not serve with the setup page,
        // so this is checked by content rather than by status code.
        Assert.Contains("Set this iPad up", body, StringComparison.Ordinal);
    }

    private sealed record ClaimResponse(object[] Guests);
}
