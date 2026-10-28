using System.Security.Cryptography;

namespace Photobooth.Delivery;

/// <summary>
/// Keeps a secret on disk readable only by the person running the booth.
///
/// On Windows that is DPAPI: encrypted for this user on this machine, so a copy
/// of the file taken off a stolen laptop is useless. DPAPI does not exist
/// elsewhere, so on macOS and Linux the bytes are written as they are, with
/// permissions that let only this user read them. That leans on the account
/// password and disk encryption (FileVault) rather than on a key bound to the
/// machine -- weaker than DPAPI, and the reason to leave FileVault on for a
/// Mac that runs the booth.
/// </summary>
public static class SecretProtector
{
    /// <summary>
    /// Write <paramref name="secret"/> to <paramref name="path"/>. The folder
    /// must already exist.
    /// </summary>
    /// <param name="entropy">
    /// Extra bytes DPAPI mixes in, so another app running as the same user
    /// cannot decrypt the file. Unused off Windows.
    /// </param>
    public static void Write(string path, byte[] secret, byte[] entropy)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllBytes(
                path,
                ProtectedData.Protect(secret, entropy, DataProtectionScope.CurrentUser));
            return;
        }

        const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        // Created owner-only, so there is no moment when it is world-readable.
        // An existing file keeps its old mode when truncated, hence the second call.
        using (var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = OwnerOnly,
        }))
        {
            stream.Write(secret);
        }

        File.SetUnixFileMode(path, OwnerOnly);
    }

    /// <summary>
    /// Read back what <see cref="Write"/> stored. On Windows a file DPAPI cannot
    /// decrypt -- copied from another machine or account -- throws
    /// <see cref="CryptographicException"/>. Elsewhere there is nothing to decrypt,
    /// so a damaged file surfaces when the caller tries to parse it.
    /// </summary>
    public static byte[] Read(string path, byte[] entropy)
    {
        var stored = File.ReadAllBytes(path);

        return OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(stored, entropy, DataProtectionScope.CurrentUser)
            : stored;
    }
}
