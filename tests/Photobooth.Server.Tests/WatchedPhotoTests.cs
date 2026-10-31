using System.Net;

namespace Photobooth.Server.Tests;

/// <summary>
/// EOS Utility files shots under a dated subfolder. The operator console asks for
/// each shot by name, so the name has to be found there as well.
/// </summary>
public sealed class WatchedPhotoTests : IClassFixture<BoothProcess>
{
    private readonly BoothProcess _booth;

    public WatchedPhotoTests(BoothProcess booth) => _booth = booth;

    [Fact]
    public async Task A_photo_in_a_dated_subfolder_is_served_by_name()
    {
        var dated = Path.Combine(_booth.WatchFolder, "2026_10_28");
        Directory.CreateDirectory(dated);
        var bytes = new byte[4096];
        Random.Shared.NextBytes(bytes);
        await File.WriteAllBytesAsync(Path.Combine(dated, "IMG_0042.JPG"), bytes);

        var response = await _booth.Operator.GetAsync("/api/photos/IMG_0042.JPG");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("IMG_*.JPG")]
    [InlineData("IMG_004?.JPG")]
    public async Task A_wildcard_is_not_a_photo_name(string name)
    {
        var response = await _booth.Operator.GetAsync("/api/photos/" + Uri.EscapeDataString(name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_photo_is_not_found()
    {
        var response = await _booth.Operator.GetAsync("/api/photos/IMG_9999.JPG");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
