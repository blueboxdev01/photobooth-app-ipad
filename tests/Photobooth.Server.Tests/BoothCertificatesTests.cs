using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The booth's certificate authority.
///
/// Every assertion here is a rule iOS enforces. Break one and Safari refuses the
/// certificate <b>even after the root has been trusted</b>, with an error that
/// does not say which rule was broken -- so these are pinned rather than assumed,
/// because the alternative is discovering it on an iPad at a venue.
/// </summary>
public sealed class BoothCertificatesTests : IDisposable
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), $"pb-ca-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private BoothCertificates Load() => BoothCertificates.Load(_folder);

    // --- what iOS demands of the server certificate --------------------------

    /// <summary>
    /// Apple caps server certificates at 398 days for anything issued since
    /// September 2020. A ten-year leaf looks fine everywhere except the one
    /// device this feature exists for.
    /// </summary>
    [Fact]
    public void The_leaf_lasts_no_longer_than_iOS_allows()
    {
        using var leaf = Load().IssueServerCertificate();

        var days = (leaf.NotAfter - leaf.NotBefore).TotalDays;

        Assert.True(days <= 398, $"leaf is valid for {days:F0} days; iOS rejects over 398");
        Assert.True(days > 300, $"leaf is only valid for {days:F0} days, which is needlessly short");
    }

    /// <summary>
    /// iOS has ignored the Common Name since iOS 13. Without subject alternative
    /// names the certificate simply does not match the address, however correct
    /// it otherwise looks.
    /// </summary>
    [Fact]
    public void The_leaf_names_the_machine_in_its_subject_alternative_names()
    {
        using var leaf = Load().IssueServerCertificate();

        var san = leaf.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .SingleOrDefault();

        Assert.NotNull(san);

        var dns = san.EnumerateDnsNames().ToArray();
        Assert.Contains(Environment.MachineName, dns);
        Assert.Contains($"{Environment.MachineName}.local", dns);
        Assert.Contains("localhost", dns);
    }

    /// <summary>
    /// The mDNS name is the one that survives moving to a venue on a different
    /// network, where a baked-in address would not.
    /// </summary>
    [Fact]
    public void The_preferred_host_is_the_mdns_name()
    {
        Assert.Equal($"{Environment.MachineName}.local", BoothCertificates.PreferredHost());
        Assert.Contains(BoothCertificates.PreferredHost(), BoothCertificates.DnsNames());
    }

    [Fact]
    public void The_leaf_covers_the_addresses_this_machine_answers_on()
    {
        using var leaf = Load().IssueServerCertificate();

        var san = leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
        var addresses = san.EnumerateIPAddresses().ToArray();

        Assert.Contains(IPAddress.Loopback, addresses);

        foreach (var local in BoothCertificates.Addresses())
        {
            Assert.Contains(local, addresses);
        }
    }

    [Fact]
    public void The_leaf_is_marked_for_server_authentication()
    {
        using var leaf = Load().IssueServerCertificate();

        var eku = leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();

        Assert.Contains(eku.EnhancedKeyUsages.Cast<Oid>(), o => o.Value == ServerAuthOid);
    }

    [Fact]
    public void The_leaf_is_signed_with_sha256_and_a_2048_bit_key()
    {
        using var leaf = Load().IssueServerCertificate();

        Assert.Contains("sha256", leaf.SignatureAlgorithm.FriendlyName!, StringComparison.OrdinalIgnoreCase);

        using var key = leaf.GetRSAPublicKey();
        Assert.NotNull(key);
        Assert.True(key.KeySize >= 2048, $"key is only {key.KeySize} bits");
    }

    /// <summary>A leaf that claims to be a CA is a leaf iOS will not serve with.</summary>
    [Fact]
    public void The_leaf_is_not_itself_an_authority()
    {
        using var leaf = Load().IssueServerCertificate();

        var basic = leaf.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        Assert.False(basic.CertificateAuthority);
    }

    [Fact]
    public void The_leaf_carries_its_private_key_so_Kestrel_can_serve_it()
    {
        using var leaf = Load().IssueServerCertificate();

        Assert.True(leaf.HasPrivateKey, "Kestrel cannot serve a certificate with no key");
    }

    // --- the root ------------------------------------------------------------

    /// <summary>iOS only offers "enable full trust" for a certificate that is a root.</summary>
    [Fact]
    public void The_root_is_a_certificate_authority()
    {
        var basic = Load().Authority.Extensions
            .OfType<X509BasicConstraintsExtension>().Single();

        Assert.True(basic.CertificateAuthority);
    }

    [Fact]
    public void The_root_can_sign_certificates()
    {
        var usage = Load().Authority.Extensions
            .OfType<X509KeyUsageExtension>().Single();

        Assert.True(usage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign));
    }

    /// <summary>
    /// The root is installed by hand on every iPad, so it outliving the leaf many
    /// times over is the whole point.
    /// </summary>
    [Fact]
    public void The_root_lasts_years()
    {
        var authority = Load().Authority;

        Assert.True(
            (authority.NotAfter - authority.NotBefore).TotalDays > 365 * 5,
            "a short-lived root would mean re-trusting every iPad");
    }

    [Fact]
    public void The_leaf_chains_to_the_root()
    {
        var booth = Load();
        using var leaf = booth.IssueServerCertificate();

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(booth.Authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var ok = chain.Build(leaf);

        Assert.True(
            ok,
            "leaf does not chain to the booth root: "
            + string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation.Trim())));
    }

    // --- what makes it survive a venue change --------------------------------

    /// <summary>
    /// The reason for splitting root and leaf at all. The iPad's trust lives in
    /// the root, so the root must come back identical across restarts -- if it
    /// changed, every device would have to trust the booth again.
    /// </summary>
    [Fact]
    public void The_root_is_the_same_one_after_a_restart()
    {
        var first = Load().AuthorityThumbprint;
        var second = Load().AuthorityThumbprint;

        Assert.Equal(first, second);
    }

    /// <summary>
    /// And the leaf is genuinely reissued, which is what picks up a new address
    /// at a new venue without anybody doing anything.
    /// </summary>
    [Fact]
    public void Each_start_issues_a_fresh_leaf()
    {
        var booth = Load();

        using var one = booth.IssueServerCertificate();
        using var two = booth.IssueServerCertificate();

        Assert.NotEqual(one.Thumbprint, two.Thumbprint);
        Assert.NotEqual(one.SerialNumber, two.SerialNumber);
    }

    [Fact]
    public void The_root_is_not_readable_on_disk()
    {
        Load();

        var file = Directory.EnumerateFiles(_folder, "booth-ca.*").Single();
        var raw = File.ReadAllBytes(file);

        // A PKCS#12 blob starts with an ASN.1 SEQUENCE. Encrypted at rest, it
        // should not: this is the cheapest check that DPAPI actually ran.
        Assert.NotEqual(0x30, raw[0]);
    }

    /// <summary>
    /// A root copied from another machine cannot be decrypted here. That must
    /// read as "make a new one", not as a booth that will not start.
    /// </summary>
    [Fact]
    public void A_root_this_machine_cannot_decrypt_is_replaced_rather_than_fatal()
    {
        Load();
        var file = Directory.EnumerateFiles(_folder, "booth-ca.*").Single();
        File.WriteAllBytes(file, [9, 9, 9, 9, 9, 9, 9, 9]);

        var recovered = BoothCertificates.Load(_folder);

        Assert.NotNull(recovered.Authority);
        Assert.True(recovered.AuthorityExpires > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void The_root_is_exported_in_the_form_an_iPad_downloads()
    {
        var der = Load().AuthorityDer;

        // DER, not PKCS#12 and not PEM: an ASN.1 SEQUENCE, and loadable as a
        // bare certificate.
        Assert.Equal(0x30, der[0]);

        using var reloaded = X509CertificateLoader.LoadCertificate(der);
        Assert.False(reloaded.HasPrivateKey, "the root's private key must never leave the booth");
    }
}
