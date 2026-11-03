using System.Diagnostics;
using System.IO;
using AzureOpenAI_CLI.Mcp;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E18 -- MCP stdio shutdown. <c>McpStdioClient.Dispose</c> used to SIGKILL the server at once, so a server (or an
/// HTTP bridge) never saw its input close and could not release what it held. Found while connecting hopper to
/// scouter: its host admits 8 observer sessions, and every hopper run leaked one, so the ninth failed with HTTP 429.
/// The stub servers here are tiny Python scripts, so the tests need no network and no model.
/// </summary>
public class McpClientShutdownTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mcp-shutdown-" + Guid.NewGuid().ToString("N"));
    private readonly int _savedGrace = McpStdioClient.GracefulShutdownMs;

    public McpClientShutdownTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        McpStdioClient.GracefulShutdownMs = _savedGrace;
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static bool HavePython()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("python3", "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            p!.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    // A minimal MCP stdio server. `mode` decides what it does when its input closes:
    //   cleanup  -> write the marker file and exit        (a well-behaved server or bridge)
    //   ignore   -> keep running forever                  (a server that never notices)
    private string WriteStub(string mode)
    {
        var script = Path.Combine(_dir, "stub-" + mode + ".py");
        File.WriteAllText(script, """
            import json, os, sys, time
            marker, mode = sys.argv[1], sys.argv[2]
            open(marker + ".pid", "w").write(str(os.getpid()))   # lets a test find this exact process on any platform
            for line in sys.stdin:
                try: m = json.loads(line)
                except ValueError: continue
                if m.get("method") == "initialize":
                    print(json.dumps({"jsonrpc": "2.0", "id": m["id"], "result": {"protocolVersion": "2024-11-05", "capabilities": {}, "serverInfo": {"name": "stub", "version": "1"}}}), flush=True)
            # stdin reached EOF: the client closed our input
            if mode == "cleanup":
                open(marker, "w").write("released")
                sys.exit(0)
            while True: time.sleep(1)
            """);
        return script;
    }

    [Fact]
    public async Task Dispose_ClosesTheServersInput_SoItCanCleanUp()
    {
        if (!HavePython()) return; // no python3 on this host; the other platforms' runs cover it
        var marker = Path.Combine(_dir, "released.marker");
        var client = await McpStdioClient.ConnectSubprocessAsync($"stub=python3 {WriteStub("cleanup")} {marker} cleanup", 10);

        client.Dispose();

        Assert.True(File.Exists(marker), "the server never saw its input close: it was killed before it could clean up");
    }

    [Fact]
    public async Task Dispose_ReturnsPromptly_WhenTheServerExitsOnItsOwn()
    {
        if (!HavePython()) return;
        McpStdioClient.GracefulShutdownMs = 5000; // a generous grace period must not be spent when the server exits at once
        var client = await McpStdioClient.ConnectSubprocessAsync($"stub=python3 {WriteStub("cleanup")} {Path.Combine(_dir, "m")} cleanup", 10);

        var watch = Stopwatch.StartNew();
        client.Dispose();

        Assert.True(watch.ElapsedMilliseconds < 3000, $"Dispose waited {watch.ElapsedMilliseconds} ms for a server that exits immediately");
    }

    [Fact]
    public async Task Dispose_StillKillsAServerThatIgnoresTheClosedInput()
    {
        if (!HavePython()) return;
        McpStdioClient.GracefulShutdownMs = 300;
        var marker = Path.Combine(_dir, "m");
        var client = await McpStdioClient.ConnectSubprocessAsync($"stub=python3 {WriteStub("ignore")} {marker} ignore", 10);
        var pid = int.Parse(File.ReadAllText(marker + ".pid")); // written by the stub itself: no guessing by process name

        try
        {
            client.Dispose();

            // Give the kill a moment to land, then the process must be gone.
            var gone = false;
            for (var i = 0; i < 40 && !gone; i++)
            {
                // A killed child can linger as a zombie until it is reaped, so "gone" means exited or no longer present.
                try { gone = Process.GetProcessById(pid).HasExited; if (!gone) await Task.Delay(50); }
                catch (ArgumentException) { gone = true; }
            }
            Assert.True(gone, "a server that ignores its closed input must still be killed after the grace period");
        }
        finally
        {
            // If the fix ever regresses, do not leave the stub running for the next test or the next run.
            try { Process.GetProcessById(pid).Kill(); } catch { }
        }
    }
}
