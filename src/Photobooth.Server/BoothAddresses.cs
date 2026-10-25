using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Photobooth.Server;

/// <summary>One address this machine answers on, and the adapter it belongs to.</summary>
/// <param name="Address">The IPv4 address, as it would appear in a link.</param>
/// <param name="Adapter">The adapter's name, which is the only thing that makes
/// two private addresses tellable apart by a human: "Ethernet" and "Wi-Fi" mean
/// something to the operator, "192.168.8.100" does not.</param>
/// <param name="Kind">Ethernet, Wi-Fi, or anything else.</param>
public sealed record BoothAddress(string Address, string Adapter, string Kind);

/// <summary>
/// Which of this machine's addresses to put in a guest's QR code.
///
/// <para>
/// This used to be "the first one that is not loopback", which is not a decision
/// -- it is whatever order Windows happened to enumerate the adapters in. It
/// worked on the bench and would have failed at a venue, because the booth
/// laptop is routinely on two networks at once: the guests' router by cable, and
/// the operator's own wifi for everything else. Advertise the wrong one and the
/// QR scans perfectly, the page never loads, and nothing on screen says why.
/// </para>
///
/// <para>
/// So the operator picks, and the pick is remembered. Automatic stays as the
/// default because most booths only ever have one usable address, but it is a
/// starting point rather than an answer.
/// </para>
/// </summary>
public static class BoothAddresses
{
    /// <summary>
    /// Addresses worth offering, best guess first.
    ///
    /// Loopback is dropped because no phone can reach it, and 169.254.x is
    /// dropped because it does not mean "a network" -- it means DHCP failed and
    /// Windows made an address up. Offering either invites the operator to
    /// choose one that cannot possibly work.
    /// </summary>
    public static IReadOnlyList<BoothAddress> Usable(IEnumerable<BoothAddress> all) =>
        all.Where(a => IPAddress.TryParse(a.Address, out var parsed)
                       && !IPAddress.IsLoopback(parsed)
                       && !IsLinkLocal(parsed))
           .OrderBy(Rank)
           .ThenBy(a => a.Address, StringComparer.OrdinalIgnoreCase)
           .ToList();

    /// <summary>
    /// The address a guest link should carry.
    ///
    /// <paramref name="preferred"/> wins only while it is still one of this
    /// machine's addresses. A saved choice from the last venue is a dead address
    /// at this one, and quietly honouring it would produce exactly the silent
    /// failure this whole class exists to prevent.
    /// </summary>
    public static BoothAddress? Choose(string? preferred, IReadOnlyList<BoothAddress> usable)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var match = usable.FirstOrDefault(a =>
                string.Equals(a.Address, preferred.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return usable.FirstOrDefault();
    }

    /// <summary>Whether a saved choice is still an address this machine has.</summary>
    public static bool IsAvailable(string? preferred, IReadOnlyList<BoothAddress> usable) =>
        !string.IsNullOrWhiteSpace(preferred)
        && usable.Any(a => string.Equals(
            a.Address, preferred.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Ethernet ahead of wifi, because the setup we recommend puts the booth on
    /// the guests' router by cable and leaves the wifi radio for the guests. When
    /// a booth has both, the cable is the one guests can reach.
    /// </summary>
    private static int Rank(BoothAddress address) => address.Kind switch
    {
        "Ethernet" => 0,
        "Wi-Fi" => 1,
        _ => 2,
    };

    private static bool IsLinkLocal(IPAddress address)
    {
        var octets = address.GetAddressBytes();
        return octets.Length == 4 && octets[0] == 169 && octets[1] == 254;
    }

    /// <summary>Every IPv4 address this machine currently answers on.</summary>
    public static IReadOnlyList<BoothAddress> OnThisMachine()
    {
        var found = new List<BoothAddress>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var kind = nic.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                    NetworkInterfaceType.Ethernet
                        or NetworkInterfaceType.GigabitEthernet
                        or NetworkInterfaceType.FastEthernetT
                        or NetworkInterfaceType.FastEthernetFx => "Ethernet",
                    _ => "Other",
                };

                foreach (var address in nic.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        found.Add(new BoothAddress(address.Address.ToString(), nic.Name, kind));
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

    /// <summary>Usable addresses on this machine, best guess first.</summary>
    public static IReadOnlyList<BoothAddress> UsableOnThisMachine() => Usable(OnThisMachine());
}
