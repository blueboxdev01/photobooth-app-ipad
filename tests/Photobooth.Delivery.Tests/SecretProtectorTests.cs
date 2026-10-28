using Photobooth.Delivery;

namespace Photobooth.Delivery.Tests;

public sealed class SecretProtectorTests : IDisposable
{
    private static readonly byte[] Entropy = [1, 2, 3];

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), $"pb-secret-{Guid.NewGuid():N}");

    public SecretProtectorTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void A_secret_comes_back_as_written()
    {
        var path = Path.Combine(_folder, "secret.bin");
        byte[] secret = [10, 20, 30, 40];

        SecretProtector.Write(path, secret, Entropy);

        Assert.Equal(secret, SecretProtector.Read(path, Entropy));
    }

    [Fact]
    public void Overwriting_replaces_the_old_secret_completely()
    {
        var path = Path.Combine(_folder, "secret.bin");

        SecretProtector.Write(path, [1, 1, 1, 1, 1, 1, 1, 1], Entropy);
        SecretProtector.Write(path, [2, 2], Entropy);

        Assert.Equal<byte>([2, 2], SecretProtector.Read(path, Entropy));
    }

    [Fact]
    public void Off_Windows_only_the_owner_can_read_it()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_folder, "secret.bin");
        File.WriteAllBytes(path, [0]);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        // An existing, wider file must be tightened, not left as it was.
        SecretProtector.Write(path, [5, 6, 7], Entropy);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
