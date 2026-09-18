using Microsoft.Extensions.Configuration;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// Reconciling what the build shipped with what the operator chose.
///
/// <para>
/// This exists because the two were once worked out <b>twice</b>: the app bound
/// its ports from one object and reported its state from another, and only the
/// first had the operator's choice applied to it. So the booth served the guest
/// display correctly while Setup insisted a restart was still needed -- forever,
/// however many times you restarted. A field tester lost an afternoon to it.
/// </para>
///
/// <para>
/// The fix is that there is now one answer, produced here. These tests pin what
/// that answer is.
/// </para>
/// </summary>
public sealed class GuestDisplayOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v =>
                new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private const string Enabled = "GuestDisplay:Enabled";

    // --- the operator's choice wins -----------------------------------------

    /// <summary>
    /// The case that was broken. The build ships with this off; the operator
    /// turns it on; after a restart the booth must agree that it is on.
    /// </summary>
    [Fact]
    public void Turning_it_on_survives_a_restart()
    {
        var resolved = GuestDisplayOptions.Resolve(
            Config((Enabled, "false")), operatorChoice: true);

        Assert.True(resolved.Enabled);
    }

    [Fact]
    public void Turning_it_off_beats_a_build_that_ships_it_on()
    {
        var resolved = GuestDisplayOptions.Resolve(
            Config((Enabled, "true")), operatorChoice: false);

        Assert.False(resolved.Enabled);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void An_operator_who_never_chose_gets_what_the_build_shipped(
        string configured, bool expected)
    {
        var resolved = GuestDisplayOptions.Resolve(
            Config((Enabled, configured)), operatorChoice: null);

        Assert.Equal(expected, resolved.Enabled);
    }

    /// <summary>
    /// Binding to the network is a decision someone makes for a specific booth,
    /// never something a build starts doing because it was unzipped.
    /// </summary>
    [Fact]
    public void A_booth_that_has_been_told_nothing_at_all_stays_off()
    {
        Assert.False(GuestDisplayOptions.Resolve(Config(), operatorChoice: null).Enabled);
    }

    // --- the ports -----------------------------------------------------------

    [Fact]
    public void Ports_come_from_configuration()
    {
        var resolved = GuestDisplayOptions.Resolve(
            Config((Enabled, "true"),
                   ("GuestDisplay:CertPort", "6001"),
                   ("GuestDisplay:DisplayPort", "6002")),
            operatorChoice: null);

        Assert.Equal(6001, resolved.CertPort);
        Assert.Equal(6002, resolved.DisplayPort);
    }

    /// <summary>
    /// The operator's switch says whether to serve, never on which ports. A
    /// choice that silently moved the ports would leave every QR already printed
    /// pointing at nothing.
    /// </summary>
    [Fact]
    public void The_operators_choice_does_not_disturb_the_ports()
    {
        var shipped = GuestDisplayOptions.Resolve(Config(), operatorChoice: null);
        var chosen = GuestDisplayOptions.Resolve(Config(), operatorChoice: true);

        Assert.Equal(shipped.CertPort, chosen.CertPort);
        Assert.Equal(shipped.DisplayPort, chosen.DisplayPort);
    }

    /// <summary>
    /// Resolving twice from the same inputs must agree with itself. The original
    /// bug was precisely two answers to one question, so this is the property
    /// that was actually violated.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void Asking_twice_gives_the_same_answer(bool? choice)
    {
        var configuration = Config((Enabled, "false"), ("GuestDisplay:CertPort", "7001"));

        var first = GuestDisplayOptions.Resolve(configuration, choice);
        var second = GuestDisplayOptions.Resolve(configuration, choice);

        Assert.Equal(first.Enabled, second.Enabled);
        Assert.Equal(first.CertPort, second.CertPort);
        Assert.Equal(first.DisplayPort, second.DisplayPort);
    }
}
