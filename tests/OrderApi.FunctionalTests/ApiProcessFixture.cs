using System.Diagnostics;
using Xunit;

namespace OrderApi.FunctionalTests;

// Two modes, both genuinely end-to-end (no TestServer shortcuts either way):
//
//   ORDERAPI_BASE_URL   - point at an already-running instance over the network
//                         (e.g. the real Service inside an OpenShift cluster:
//                         http://order-api:8080). No process is spawned; this
//                         is what runs as an OpenShift Job hitting the actual
//                         deployed pod.
//
//   ORDERAPI_DLL_PATH   - spawn the published app as a local OS process on a
//                         real port and hit that instead. This is what CI/
//                         local dev uses when there's nothing already deployed.
//
// Exactly one of the two should be set.
public class ApiProcessFixture : IAsyncLifetime
{
    private Process? _process;
    private const string LocalBaseUrl = "http://localhost:5299";

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var remoteBaseUrl = Environment.GetEnvironmentVariable("ORDERAPI_BASE_URL");
        var dllPath = Environment.GetEnvironmentVariable("ORDERAPI_DLL_PATH");

        if (!string.IsNullOrWhiteSpace(remoteBaseUrl))
        {
            Client = new HttpClient { BaseAddress = new Uri(remoteBaseUrl) };
        }
        else if (!string.IsNullOrWhiteSpace(dllPath))
        {
            if (!File.Exists(dllPath))
                throw new FileNotFoundException($"OrderApi.dll not found at '{dllPath}'. Did you publish it first?", dllPath);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{dllPath}\" --urls {LocalBaseUrl}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            _process.Start();

            Client = new HttpClient { BaseAddress = new Uri(LocalBaseUrl) };
        }
        else
        {
            throw new InvalidOperationException(
                "Set either ORDERAPI_BASE_URL (to test an already-running instance, " +
                "e.g. in OpenShift) or ORDERAPI_DLL_PATH (to spawn a local published " +
                "build) before running functional tests.");
        }

        // Poll /health until it's genuinely reachable -- for a spawned local
        // process this covers process startup; for a remote target this
        // covers a pod that's still rolling out or a Route that's still
        // propagating.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            if (_process is { HasExited: true })
            {
                var stderr = await _process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"OrderApi process exited early. Stderr:\n{stderr}");
            }

            try
            {
                var response = await Client.GetAsync("/health");
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
            await Task.Delay(500);
        }

        throw new TimeoutException("OrderApi did not become healthy within 30 seconds.", lastError);
    }

    public Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.Dispose();
        }
        Client.Dispose();
        return Task.CompletedTask;
    }
}
