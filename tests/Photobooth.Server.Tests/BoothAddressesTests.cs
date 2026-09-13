using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// Which address a guest's QR advertises.
///
/// This is the quiet failure: a QR built on the wrong address scans perfectly,
/// opens a browser, and then hangs on a page that never loads. Nothing reports
/// an error, so it looks like the router's fault -- which is why the choice is
/// pinned here rather than left to adapter enumeration order.
/// </summary>
public sealed class BoothAddressesTests
{
    private static BoothAddress Wifi(string address) => new(address, "Wi-Fi", "Wi-Fi");
    private static BoothAddress Wired(string address) => new(address, "Ethernet", "Ethernet");

    // --- what is worth offering ----------------------------------------------

    /// <summary>
    /// 169.254.x is not a network. It is what Windows invents when DHCP fails,
    /// and nothing else can reach it -- so it must never be offered as a choice.
    /// </summary>
    [Fact]
    public void An_address_windows_invented_because_dhcp_failed_is_not_offered()
    {
        var usable = BoothAddresses.Usable([
            Wired("169.254.12.9"),
            Wifi("192.168.1.34"),
        ]);

        Assert.Equal(["192.168.1.34"], usable.Select(a => a.Address));
    }

    [Fact]
    public void Loopback_is_not_offered_because_no_phone_can_reach_it()
    {
        var usable = BoothAddresses.Usable([
            new("127.0.0.1", "Loopback", "Other"),
            Wifi("192.168.1.34"),
        ]);

        Assert.Equal(["192.168.1.34"], usable.Select(a => a.Address));
    }

    [Fact]
    public void Anything_unparseable_is_dropped_rather_than_offered()
    {
        Assert.Empty(BoothAddresses.Usable([new("not-an-address", "Ethernet", "Ethernet")]));
    }

    /// <summary>
    /// The recommended setup puts the booth on the guests' router by cable and
    /// leaves the wifi radio to the guests, so when a booth has both, the cable
    /// is the one a guest can reach.
    /// </summary>
    [Fact]
    public void A_cable_is_guessed_ahead_of_wifi()
    {
        var usable = BoothAddresses.Usable([
            Wifi("192.168.1.34"),
            Wired("192.168.8.100"),
        ]);

        Assert.Equal("192.168.8.100", usable[0].Address);
    }

    [Fact]
    public void The_order_does_not_depend_on_the_order_they_arrived_in()
    {
        string[] Ordered(IEnumerable<BoothAddress> input) =>
            BoothAddresses.Usable(input).Select(a => a.Address).ToArray();

        BoothAddress[] all = [Wifi("192.168.1.34"), Wired("192.168.8.100"), Wifi("10.0.0.5")];

        Assert.Equal(Ordered(all), Ordered(all.Reverse()));
    }

    // --- the operator's own choice -------------------------------------------

    [Fact]
    public void The_operators_choice_beats_the_guess()
    {
        var usable = BoothAddresses.Usable([Wired("192.168.8.100"), Wifi("192.168.1.34")]);

        // The guess would be the cable; the operator says otherwise.
        Assert.Equal("192.168.1.34", BoothAddresses.Choose("192.168.1.34", usable)?.Address);
    }

    /// <summary>
    /// The case that matters at the second venue. A choice saved last week names
    /// an address this machine no longer has; honouring it would advertise a
    /// link nothing can reach, so the guess takes over instead.
    /// </summary>
    [Fact]
    public void A_choice_from_another_venue_is_not_honoured_once_it_is_gone()
    {
        var usable = BoothAddresses.Usable([Wifi("192.168.1.34")]);

        Assert.Equal("192.168.1.34", BoothAddresses.Choose("10.20.30.40", usable)?.Address);
        Assert.False(BoothAddresses.IsAvailable("10.20.30.40", usable));
        Assert.True(BoothAddresses.IsAvailable("192.168.1.34", usable));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_choice_means_choose_automatically(string? preferred)
    {
        var usable = BoothAddresses.Usable([Wired("192.168.8.100"), Wifi("192.168.1.34")]);

        Assert.Equal("192.168.8.100", BoothAddresses.Choose(preferred, usable)?.Address);
        Assert.False(BoothAddresses.IsAvailable(preferred, usable));
    }

    [Fact]
    public void Surrounding_space_does_not_lose_a_saved_choice()
    {
        var usable = BoothAddresses.Usable([Wired("192.168.8.100"), Wifi("192.168.1.34")]);

        Assert.Equal("192.168.1.34", BoothAddresses.Choose("  192.168.1.34  ", usable)?.Address);
        Assert.True(BoothAddresses.IsAvailable(" 192.168.1.34 ", usable));
    }

    /// <summary>A booth with no network at all must not crash the guest screen.</summary>
    [Fact]
    public void A_booth_with_nothing_usable_chooses_nothing()
    {
        Assert.Null(BoothAddresses.Choose(null, []));
        Assert.Null(BoothAddresses.Choose("192.168.1.34", []));
    }

    // --- the link that is actually built -------------------------------------

    [Fact]
    public void A_booth_with_no_network_still_builds_a_link_rather_than_throwing()
    {
        // Whatever this machine has, the fallback must produce something.
        Assert.False(string.IsNullOrWhiteSpace(GuestGalleryLinks.GuestHost("10.20.30.40")));
    }

    /// <summary>
    /// The whole point, end to end: a chosen address reaches the QR's URL. Uses
    /// a real address of the machine the tests run on, so it proves the wiring
    /// rather than the fallback.
    /// </summary>
    [Fact]
    public void A_chosen_address_is_what_the_guest_link_carries()
    {
        var usable = BoothAddresses.UsableOnThisMachine();

        if (usable.Count == 0)
        {
            // A build agent with no network. The fallback is then the claim
            // worth making, rather than skipping and reporting a pass.
            Assert.Equal("127.0.0.1", GuestGalleryLinks.GuestHost(null));
            return;
        }

        // The last one, so a pass cannot come from it also being the guess.
        var chosen = usable[^1].Address;

        Assert.Equal(chosen, GuestGalleryLinks.GuestHost(chosen));
        Assert.Contains(chosen, GuestGalleryLinks.PhotosUrl(chosen, 5101, "tok"));
    }
}
