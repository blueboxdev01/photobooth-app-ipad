using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Photobooth.Server;

/// <summary>
/// The booth's own certificate authority, so an iPad can be the guest display.
///
/// A browser will not give a page the camera unless the page is a secure
/// context, and the posing mirror is a camera. No public authority will issue a
/// certificate for <c>192.168.1.50</c> or <c>booth.local</c>, so the booth issues
/// its own and the iPad is told once to trust it.
///
/// <para>
/// <b>A root and a leaf, not one self-signed certificate.</b> iOS offers its
/// "enable full trust" switch for root certificates, and a root is the thing
/// worth installing once and never again. So the root here lives for years and
/// stays put, while the server certificate it signs is thrown away and reissued
/// on every start. That is what lets the booth move to a venue on a different
/// network: new address, new leaf, same trusted root, nothing to redo on the
/// iPad.
/// </para>
///
/// <para>
/// <b>iOS is strict about the leaf</b> and rejects one that breaks any of the
/// following -- even after the root is trusted, and with an error that does not
/// say which:
/// </para>
/// <list type="bullet">
/// <item>at most 398 days of validity (Apple's rule since September 2020)</item>
/// <item>subject alternative names; Common Name has been ignored since iOS 13</item>
/// <item>an extended key usage of serverAuth</item>
/// <item>SHA-256, and RSA 2048 or better</item>
/// </list>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BoothCertificates
{
    /// <summary>Apple's cap is 398. A day short of it avoids any rounding argument.</summary>
    private const int LeafDays = 397;

    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    private static readonly byte[] Entropy =
        System.Text.Encoding.UTF8.GetBytes("Photobooth.Server.BoothCertificates");

    private BoothCertificates(X509Certificate2 authority) => Authority = authority;

    /// <summary>The root. Its public half is what gets installed on the iPad.</summary>
    public X509Certificate2 Authority { get; }

    /// <summary>
    /// The root in DER form -- what an iPad expects to download, and what Safari
    /// will offer to install as a configuration profile.
    /// </summary>
    public byte[] AuthorityDer => Authority.Export(X509ContentType.Cert);

    /// <summary>
    /// A stable fingerprint for the console to show. If it ever changes, every
    /// iPad has to trust the booth again, and this is how anyone would know.
    /// </summary>
    public string AuthorityThumbprint => Authority.Thumbprint;

    public DateTimeOffset AuthorityExpires => Authority.NotAfter;

    /// <summary>
    /// Load the booth's root, creating one the first time.
    ///
    /// The private key is encrypted for this Windows user: a local root that can
    /// be copied off a laptop is a root that can impersonate the booth to every
    /// device trusting it.
    /// </summary>
    public static BoothCertificates Load(string dataFolder)
    {
        Directory.CreateDirectory(dataFolder);
        var path = Path.Combine(dataFolder, "booth-ca.bin");

        if (File.Exists(path))
        {
            try
            {
                var pfx = ProtectedData.Unprotect(
                    File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);

                var existing = X509CertificateLoader.LoadPkcs12(
                    pfx, null, X509KeyStorageFlags.Exportable);

                // An expired root would be quietly useless. Replacing it means
                // every iPad must trust the booth again, which is why the console
                // shows the thumbprint.
                if (existing.NotAfter > DateTimeOffset.UtcNow.AddDays(30))
                {
                    return new BoothCertificates(existing);
                }
            }
            catch (CryptographicException)
            {
                // Copied from another machine or another Windows account, so
                // DPAPI cannot read it. Start again rather than refuse to boot.
            }
        }

        var created = CreateAuthority();
        File.WriteAllBytes(
            path,
            ProtectedData.Protect(
                created.Export(X509ContentType.Pfx),
                Entropy,
                DataProtectionScope.CurrentUser));

        return new BoothCertificates(created);
    }

    /// <summary>
    /// A server certificate for whatever this machine is currently called and
    /// currently addressed as.
    ///
    /// Reissued on every start rather than cached and validated. Generating one
    /// costs milliseconds, and "is the stored certificate still right for this
    /// network?" is a question with several wrong answers and no cheap right one.
    /// </summary>
    public X509Certificate2 IssueServerCertificate()
    {
        using var key = RSA.Create(2048);

        var request = new CertificateRequest(
            $"CN={Environment.MachineName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid(ServerAuthOid)], true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var names = new SubjectAlternativeNameBuilder();
        foreach (var dns in DnsNames())
        {
            names.AddDnsName(dns);
        }

        foreach (var ip in Addresses())
        {
            names.AddIpAddress(ip);
        }

        request.CertificateExtensions.Add(names.Build());

        var now = DateTimeOffset.UtcNow;
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;      // keep it positive; some clients reject otherwise

        using var issued = request.Create(
            Authority, now.AddMinutes(-5), now.AddDays(LeafDays), serial);

        // Kestrel needs the certificate to carry its private key, and on Windows
        // one built this way only keeps the key through a PKCS#12 round trip.
        using var withKey = issued.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(
            withKey.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// Every name this machine might be reached by.
    ///
    /// <c>.local</c> is the one that matters: iPads resolve mDNS, and a name
    /// survives the address changing at the next venue where a baked-in IP would
    /// not.
    /// </summary>
    public static IReadOnlyList<string> DnsNames()
    {
        var host = Environment.MachineName;
        return [host, $"{host}.local", "localhost"];
    }

    /// <summary>Every IPv4 address this machine currently answers on.</summary>
    public static IReadOnlyList<IPAddress> Addresses()
    {
        var found = new List<IPAddress> { IPAddress.Loopback };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var address in nic.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !found.Contains(address.Address))
                    {
                        found.Add(address.Address);
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
            // A machine with no usable adapter still serves localhost.
        }

        return found;
    }

    /// <summary>
    /// What to point an iPad at. The mDNS name rather than a number, so the URL
    /// printed on the Setup page keeps working at the next venue.
    /// </summary>
    public static string PreferredHost() => $"{Environment.MachineName}.local";

    private static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);

        var request = new CertificateRequest(
            $"CN=Photobooth booth authority ({Environment.MachineName}), O=Photobooth",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var now = DateTimeOffset.UtcNow;

        // Years, deliberately. This is installed by hand on every iPad, and
        // Apple's 398-day rule governs server certificates, not roots.
        var authority = request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(10));

        return X509CertificateLoader.LoadPkcs12(
            authority.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }
}
