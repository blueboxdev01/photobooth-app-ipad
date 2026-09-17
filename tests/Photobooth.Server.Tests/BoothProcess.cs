using System.Diagnostics;
using System.Net.Sockets;

namespace Photobooth.Server.Tests;

/// <summary>
/// A real booth, started as its own process on its own ports, against its own
/// temporary folders.
///
/// <para>
/// Heavier than a <c>TestServer</c>, and deliberately so. The guest display's
/// security boundary is decided by the <b>port a connection arrived on</b>, and
/// an in-memory test server has no ports -- so the one thing worth testing here
/// would be the thing that got replaced. Everything else in this file is
/// pointless without that.
/// </para>
/// </summary>
public sealed class BoothProcess : IAsyncLifetime
{
    private Process? _process;
    private string _data = string.Empty;

    public int OperatorPort { get; private set; }
    public int CertPort { get; private set; }
    public int DisplayPort { get; private set; }

    public HttpClient Operator { get; private set; } = null!;

    /// <summary>
    /// The HTTPS display port, exactly as the iPad reaches it. The booth signs
    /// its own certificate, so this client is told not to mind.
    /// </summary>
    public HttpClient Display { get; private set; } = null!;

    public HttpClient Guest { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        OperatorPort = FreePort();
        CertPort = FreePort();
        DisplayPort = FreePort();

        _data = Path.Combine(Path.GetTempPath(), $"pb-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_data);

        var server = ServerBinary();

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(server)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add(server);

        start.Environment["Urls"] = $"http://localhost:{OperatorPort}";
        start.Environment["GuestDisplay__Enabled"] = "true";
        start.Environment["GuestDisplay__CertPort"] = CertPort.ToString();
        start.Environment["GuestDisplay__DisplayPort"] = DisplayPort.ToString();
        start.Environment["Archive__Folder"] = Path.Combine(_data, "sessions");
        start.Environment["Camera__WatchFolder__Path"] = Path.Combine(_data, "watch");

        _process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {server}.");

        // Drained, or a full pipe buffer stalls the booth mid-test.
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();

        Operator = new HttpClient { BaseAddress = new Uri($"http://localhost:{OperatorPort}") };
        Guest = new HttpClient { BaseAddress = new Uri($"http://localhost:{CertPort}") };
        Display = new HttpClient(Insecure())
        {
            BaseAddress = new Uri($"https://localhost:{DisplayPort}"),
        };

        await WaitUntilListeningAsync();
    }

    /// <summary>
    /// The booth signs its own certificate for an address no public authority
    /// would ever vouch for, which is the entire point of it. An iPad is told
    /// once to trust the booth; a test client is told here.
    /// </summary>
    public static HttpClientHandler Insecure() => new()
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
    };

    private async Task WaitUntilListeningAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        Exception? last = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
            {
                throw new InvalidOperationException(
                    $"The booth exited with code {_process.ExitCode} before it was listening.");
            }

            try
            {
                var response = await Operator.GetAsync("/api/state");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The booth never answered on port {OperatorPort}. Last error: {last?.Message}");
    }

    /// <summary>
    /// The built server, found from this test assembly's own output so it
    /// follows Debug and Release without being told which.
    /// </summary>
    private static string ServerBinary()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        // ...\tests\Photobooth.Server.Tests\bin\<config>\<tfm>
        var tfm = here.Name;
        var configuration = here.Parent!.Name;

        var root = here;
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src")))
        {
            root = root.Parent;
        }

        if (root is null)
        {
            throw new InvalidOperationException(
                $"Could not find the repository root above {AppContext.BaseDirectory}.");
        }

        var path = Path.Combine(
            root.FullName, "src", "Photobooth.Server", "bin", configuration, tfm,
            "Photobooth.Server.dll");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The server has not been built at {path}. Build the solution first.", path);
        }

        return path;
    }

    /// <summary>A port nothing is using, so parallel runs do not collide.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public Task DisposeAsync()
    {
        Operator?.Dispose();
        Display?.Dispose();
        Guest?.Dispose();

        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10_000);
            }
        }
        catch { /* best effort */ }

        _process?.Dispose();

        try { Directory.Delete(_data, recursive: true); } catch { /* best effort */ }

        return Task.CompletedTask;
    }
}
