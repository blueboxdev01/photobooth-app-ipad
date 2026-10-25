using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Photobooth.Core;

namespace Photobooth.Core.Tests;

file sealed class FakeTemplateProvider(int slots) : ITemplateProvider
{
    public StripTemplate Current { get; } = new(
        "fake",
        new TemplateCanvas(600, 1800),
        [.. Enumerable.Range(0, slots).Select(i => new TemplateSlot(0, i * 0.3, 1, 0.28))]);
}

/// <summary>
/// The session id the guest screen turns into a QR.
///
/// The risk it guards against is showing one guest a code for another guest's
/// photos, so what matters is that it appears only for a finished session and is
/// gone the moment the next one starts.
/// </summary>
public class SessionTokenTests
{
    private const int Shots = 2;
    private const int Countdown = 3;

    private static (SessionEngine Engine, FakeTimeProvider Time) Build()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero));
        return (
            new SessionEngine(
                Options.Create(new SessionSettings
                {
                    CountdownSeconds = Countdown,
                    NoPhotoTimeoutSeconds = 20,
                }),
                new FakeTemplateProvider(Shots),
                NullLogger<SessionEngine>.Instance,
                time),
            time);
    }

    private static CapturedPhoto Photo(int n) =>
        new($@"C:\watch\IMG_{n:0000}.JPG", $"IMG_{n:0000}.JPG", 250_000, DateTimeOffset.UtcNow);

    private static SessionEngine Finished(out FakeTimeProvider time, string token = "tok123")
    {
        var (engine, clock) = Build();
        time = clock;

        engine.Arm();
        for (var i = 1; i <= Shots; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(Countdown));
            engine.SubmitPhoto(Photo(i));
        }

        engine.Accept();
        engine.CompleteComposing("/strip.jpg", "2026-09-13_1200_abc", token);
        return engine;
    }

    [Fact]
    public void A_finished_session_carries_its_token()
    {
        var engine = Finished(out _);

        Assert.Equal(SessionState.Done, engine.Snapshot.State);
        Assert.Equal("tok123", engine.Snapshot.Token);
    }

    /// <summary>Nothing to offer before there is a session to offer it for.</summary>
    [Fact]
    public void There_is_no_token_before_a_session_finishes()
    {
        var (engine, time) = Build();
        Assert.Null(engine.Snapshot.Token);

        engine.Arm();
        Assert.Null(engine.Snapshot.Token);

        time.Advance(TimeSpan.FromSeconds(Countdown));
        engine.SubmitPhoto(Photo(1));
        Assert.Null(engine.Snapshot.Token);
    }

    /// <summary>
    /// The one that would actually hurt: the next guest's screen must not still
    /// be offering the previous guest's photos.
    /// </summary>
    [Fact]
    public void The_next_guest_does_not_inherit_the_last_ones_token()
    {
        var engine = Finished(out _);
        Assert.NotNull(engine.Snapshot.Token);

        engine.Arm();

        Assert.Null(engine.Snapshot.Token);
    }

    [Fact]
    public void Aborting_clears_it_too()
    {
        var engine = Finished(out _);

        engine.Abort("done with it");

        Assert.Null(engine.Snapshot.Token);
    }
}
