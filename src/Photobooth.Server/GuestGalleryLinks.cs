namespace Photobooth.Server;

/// <summary>
/// What the guest screen puts in front of a guest at the end of a session.
///
/// Two codes, because there are two steps and the first is the one that loses
/// people: a phone cannot open a page on the booth until it is on the booth's
/// network.
/// </summary>
/// <param name="Enabled">Whether guests are being served their photos at all.</param>
/// <param name="JoinWifi">
/// A <c>WIFI:</c> payload. Both iOS and Android join a network straight from the
/// camera app when they see one, which turns "ask someone for the password" into
/// pointing a phone at a screen.
/// </param>
/// <param name="Ssid">Shown as text as well, for anyone joining by hand.</param>
public sealed record GuestLinks(
    bool Enabled,
    string? JoinWifi,
    string? Ssid,
    string? PhotosUrl);

public static class GuestGalleryLinks
{
    /// <summary>
    /// Build the join payload in the format phone cameras understand.
    ///
    /// The separators are meaningful inside it, so a password containing one has
    /// to be escaped or the phone reads a truncated password and simply fails to
    /// join -- with no clue as to why.
    /// </summary>
    public static string JoinPayload(string ssid, string? password)
    {
        // Backslash first, or it would go on to escape the ones added below.
        static string Escape(string value) => value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace(":", "\\:")
            .Replace("\"", "\\\"");

        var security = string.IsNullOrEmpty(password) ? "nopass" : "WPA";
        var secret = string.IsNullOrEmpty(password) ? string.Empty : Escape(password);

        return $"WIFI:T:{security};S:{Escape(ssid)};P:{secret};;";
    }

    /// <summary>
    /// The address to put in a guest's link.
    ///
    /// A number, not the mDNS name the iPad uses: Android's support for
    /// <c>.local</c> is patchy, and a guest whose phone cannot resolve it has no
    /// way to tell that is what went wrong.
    /// </summary>
    public static string GuestHost() =>
        BoothCertificates.Addresses()
            .FirstOrDefault(a => !System.Net.IPAddress.IsLoopback(a))
            ?.ToString()
        ?? "127.0.0.1";

    /// <summary>Where a guest's photos live, on the booth's network address.</summary>
    public static string PhotosUrl(string host, int port, string token) =>
        $"http://{host}:{port}{GuestGallery.Prefix}{Uri.EscapeDataString(token)}";
}
