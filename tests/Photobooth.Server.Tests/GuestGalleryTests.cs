using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Photobooth.Core;
using Photobooth.Delivery;
using Photobooth.Server;

namespace Photobooth.Server.Tests;

/// <summary>
/// The gallery a guest reaches over the booth's own wifi.
///
/// The token is the only thing standing between one guest and another guest's
/// photos, so most of this is about what a link must <b>not</b> reach.
/// </summary>
public sealed class GuestGalleryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"pb-gallery-{Guid.NewGuid():N}");

    private readonly SessionArchive _archive;

    public GuestGalleryTests() =>
        _archive = new SessionArchive(
            Options.Create(new ArchiveOptions { Folder = _root }),
            NullLogger<SessionArchive>.Instance);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>A real session on disk, with distinguishable file contents.</summary>
    private SessionRecord Save(string token, DateTimeOffset at)
    {
        Directory.CreateDirectory(_root);
        var strip = Path.Combine(_root, $"src-strip-{token}.jpg");
        File.WriteAllText(strip, $"strip of {token}");

        var captures = new List<CapturedPhoto>();
        for (var i = 1; i <= 2; i++)
        {
            var photo = Path.Combine(_root, $"src-{token}-{i}.jpg");
            File.WriteAllText(photo, $"photo {i} of {token}");
            captures.Add(new CapturedPhoto(photo, $"IMG_000{i}.JPG", 4, at));
        }

        var template = new StripTemplate(
            "fake", new TemplateCanvas(600, 1800),
            [new TemplateSlot(0, 0, 1, 0.3), new TemplateSlot(0, 0.35, 1, 0.3)]);

        return _archive.Save(token, template, captures, strip, at);
    }

    private static DefaultHttpContext Request(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private async Task<(int Status, byte[] Body)> GetAsync(string path)
    {
        var context = Request(path);
        var handled = await GuestGallery.TryServeAsync(context, _archive);

        Assert.True(handled, $"{path} should have been handled by the gallery");

        context.Response.Body.Position = 0;
        using var buffer = new MemoryStream();
        await context.Response.Body.CopyToAsync(buffer);
        return (context.Response.StatusCode, buffer.ToArray());
    }

    private static string Text(byte[] body) => System.Text.Encoding.UTF8.GetString(body);

    // --- a guest and their own photos ---------------------------------------

    [Fact]
    public async Task A_token_opens_that_session()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (status, body) = await GetAsync($"/g/{record.Token}");

        Assert.Equal(200, status);
        var html = Text(body);
        Assert.Contains("Your photos", html);
        Assert.Contains("all.zip", html);
        Assert.Contains(record.Strip, html);
    }

    [Fact]
    public async Task A_photo_downloads_with_a_name_worth_keeping()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var context = Request($"/g/{record.Token}/{record.Photos[0]}");
        await GuestGallery.TryServeAsync(context, _archive);

        Assert.Equal(200, context.Response.StatusCode);

        // "photo-1.jpg" from three booths collides in a camera roll.
        var disposition = context.Response.Headers.ContentDisposition.ToString();
        Assert.Contains("attachment", disposition);
        Assert.Contains(record.FolderName, disposition);
    }

    [Fact]
    public async Task The_zip_holds_the_strip_and_every_photo()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (status, body) = await GetAsync($"/g/{record.Token}/all.zip");

        Assert.Equal(200, status);

        using var zip = new ZipArchive(new MemoryStream(body), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.Name).ToArray();

        Assert.Equal(record.Photos.Count + 1, names.Length);
        Assert.Contains(names, n => n.EndsWith(record.Strip, StringComparison.Ordinal));
        foreach (var photo in record.Photos)
        {
            Assert.Contains(names, n => n.EndsWith(photo, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// session.json carries the token. A guest forwarding their zip to family
    /// should not be forwarding the key to their own gallery.
    /// </summary>
    [Fact]
    public async Task The_zip_does_not_include_the_session_record()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (_, body) = await GetAsync($"/g/{record.Token}/all.zip");

        using var zip = new ZipArchive(new MemoryStream(body), ZipArchiveMode.Read);
        Assert.DoesNotContain(zip.Entries, e => e.Name.Contains("session.json"));
    }

    [Fact]
    public async Task The_page_never_mentions_the_session_record()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (_, body) = await GetAsync($"/g/{record.Token}");

        Assert.DoesNotContain("session.json", Text(body));
    }

    /// <summary>
    /// The one a MemoryStream cannot catch, and running it did.
    ///
    /// ZipArchive writes the central directory from Dispose, synchronously, and
    /// Kestrel refuses synchronous writes by default. Without the opt-in, every
    /// entry streams out, the footer throws, and the guest is handed HTTP 200 and
    /// an archive their phone cannot open -- success from every angle except the
    /// only one that matters.
    /// </summary>
    [Fact]
    public async Task Building_the_zip_opts_into_the_synchronous_write_it_needs()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var context = Request($"/g/{record.Token}/all.zip");
        var control = new BodyControl();
        context.Features.Set<IHttpBodyControlFeature>(control);

        await GuestGallery.TryServeAsync(context, _archive);

        Assert.True(
            control.AllowSynchronousIO,
            "the zip's central directory is written synchronously; without this "
            + "Kestrel throws after the entries are already on the wire");
    }

    private sealed class BodyControl : IHttpBodyControlFeature
    {
        public bool AllowSynchronousIO { get; set; }
    }

    // --- what a link must not reach ------------------------------------------

    /// <summary>One guest must never be able to reach another guest's photos.</summary>
    [Fact]
    public async Task One_guests_link_does_not_reach_another_guests_photos()
    {
        var mine = Save("tokenAAA", DateTimeOffset.UtcNow);
        var theirs = Save("tokenBBB", DateTimeOffset.UtcNow.AddMinutes(5));

        var (_, body) = await GetAsync($"/g/{mine.Token}");
        var html = Text(body);

        Assert.Contains(mine.Token, html);
        Assert.DoesNotContain(theirs.Token, html);
        Assert.DoesNotContain(theirs.FolderName, html);
    }

    /// <summary>
    /// A wrong token and a session that has been cleared must look identical.
    /// Anything else tells someone guessing whether they are getting warm.
    /// </summary>
    [Fact]
    public async Task An_unknown_token_is_the_same_answer_as_a_deleted_one()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (unknownStatus, unknownBody) = await GetAsync("/g/neverexisted");

        Directory.Delete(_archive.FolderFor(record), recursive: true);
        var (goneStatus, goneBody) = await GetAsync($"/g/{record.Token}");

        Assert.Equal(404, unknownStatus);
        Assert.Equal(404, goneStatus);
        Assert.Equal(Text(unknownBody), Text(goneBody));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_token_reaches_nothing(string token)
    {
        Save("tokenAAA", DateTimeOffset.UtcNow);

        var (status, _) = await GetAsync($"/g/{token}");

        Assert.Equal(404, status);
    }

    /// <summary>
    /// The requested name is matched against the session's own list, so nothing
    /// from the URL ever reaches the filesystem and there is nothing for "../"
    /// to escape.
    /// </summary>
    [Theory]
    [InlineData("session.json")]
    [InlineData("../../../Windows/win.ini")]
    [InlineData("..%2F..%2Fsettings.json")]
    [InlineData("photo-99.jpg")]
    [InlineData("qr.png")]
    public async Task A_file_the_session_does_not_list_is_refused(string file)
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (status, _) = await GetAsync($"/g/{record.Token}/{file}");

        Assert.Equal(404, status);
    }

    /// <summary>
    /// Guessing a *folder name* must not work either -- only the token opens a
    /// session, and folder names are predictable from a date and time.
    /// </summary>
    [Fact]
    public async Task The_folder_name_is_not_a_way_in()
    {
        var record = Save("tokenAAA", DateTimeOffset.UtcNow);

        var (status, _) = await GetAsync($"/g/{record.FolderName}");

        Assert.Equal(404, status);
    }

    // --- routing -------------------------------------------------------------

    [Fact]
    public async Task Anything_outside_the_gallery_prefix_is_left_alone()
    {
        var context = Request("/operator");

        Assert.False(await GuestGallery.TryServeAsync(context, _archive));
    }

    [Theory]
    [InlineData("/g/abc", true)]
    [InlineData("/g/abc/photo-1.jpg", true)]
    [InlineData("/G/abc", true)]
    [InlineData("/gallery", false)]
    [InlineData("/operator", false)]
    [InlineData("/api/state", false)]
    public void The_prefix_is_recognised_exactly(string path, bool expected)
    {
        Assert.Equal(expected, GuestGallery.IsGalleryPath(path));
    }
}
