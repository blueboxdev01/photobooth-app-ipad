using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The two codes on the guest screen.
///
/// The join code is the step that loses people, and it fails silently: a phone
/// reading a malformed payload simply does not join, with nothing on screen to
/// say why. So the escaping is pinned rather than eyeballed.
/// </summary>
public sealed class GuestGalleryLinksTests
{
    [Fact]
    public void A_plain_network_joins()
    {
        Assert.Equal(
            "WIFI:S:Photobooth;T:WPA;P:hunter22;;",
            GuestGalleryLinks.JoinPayload("Photobooth", "hunter22"));
    }

    [Fact]
    public void An_open_network_says_so_rather_than_carrying_an_empty_password()
    {
        Assert.Equal(
            "WIFI:S:Photobooth;T:nopass;P:;;",
            GuestGalleryLinks.JoinPayload("Photobooth", null));
    }

    /// <summary>
    /// Semicolon, comma, colon and quote all mean something inside the payload.
    /// Unescaped, a password containing one is read truncated and the phone
    /// quietly fails to join.
    /// </summary>
    [Theory]
    [InlineData(";", "\\;")]
    [InlineData(",", "\\,")]
    [InlineData(":", "\\:")]
    [InlineData("\"", "\\\"")]
    public void Characters_that_would_break_the_payload_are_escaped(string raw, string expected)
    {
        var payload = GuestGalleryLinks.JoinPayload("Booth", "pw" + raw + "pw");

        Assert.Contains("P:pw" + expected + "pw;;", payload);
    }

    /// <summary>
    /// The backslash is escaped first, so it cannot go on to escape the escapes
    /// added after it -- the usual way this kind of routine produces a payload
    /// that looks right and is not.
    /// </summary>
    [Fact]
    public void A_backslash_does_not_swallow_the_other_escapes()
    {
        // The password is four characters: a, backslash, semicolon, b.
        var payload = GuestGalleryLinks.JoinPayload("Booth", "a\\;b");

        // In the payload that must become: a, backslash, backslash, backslash,
        // semicolon, b -- the backslash doubled, and the semicolon escaped.
        Assert.Contains("P:a\\\\\\;b;;", payload);
    }

    [Fact]
    public void A_network_name_is_escaped_too()
    {
        Assert.Contains("S:My\\;Booth;", GuestGalleryLinks.JoinPayload("My;Booth", "pw"));
    }

    [Fact]
    public void The_photos_link_points_at_the_booth_on_its_own_network()
    {
        Assert.Equal(
            "http://192.168.4.1:5001/g/jSKlSLVOSkLd",
            GuestGalleryLinks.PhotosUrl("192.168.4.1", 5001, "jSKlSLVOSkLd"));
    }

    /// <summary>Tokens are URL-safe by design, but a link must not assume it.</summary>
    [Fact]
    public void A_token_is_escaped_into_the_link()
    {
        Assert.Equal(
            "http://booth:5001/g/a%2Fb",
            GuestGalleryLinks.PhotosUrl("booth", 5001, "a/b"));
    }
}
