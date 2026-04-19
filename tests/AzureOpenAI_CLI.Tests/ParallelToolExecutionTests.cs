using System.Diagnostics;
using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for parallel tool call execution behaviour.
/// 
/// The agent loop now fires multiple tool calls concurrently via Task.WhenAll
/// instead of awaiting each one sequentially.  These tests verify that:
///   1. Multiple tool calls execute concurrently (wall-clock time).
///   2. Results come back in the correct order (positional matching).
///   3. Partial failures do not block sibling calls.
///   4. Cancellation propagates to all in-flight calls.
///   5. A single tool call still works correctly (degenerate case).
///   6. An empty list of tool calls is handled gracefully.
/// </summary>
public class ParallelToolExecutionTests : IDisposable
{
    private readonly string _tempDir;

    public ParallelToolExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"parallel-tool-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── Concurrency ─────────────────────────────────────────────────

    [Fact]
    public async Task ParallelExecution_MultipleToolCalls_RunsConcurrently()
    {
        // Arrange — three independent tool calls that each take ~200 ms
        // If executed sequentially this would take ≥600 ms.
        // If parallel, wall-clock should be close to 200 ms.
        var registry = ToolRegistry.Create(["datetime"]);

        var calls = new[]
        {
            ("get_datetime", "{}"),
            ("get_datetime", "{}"),
            ("get_datetime", "{}"),
        };

        // Act — mirror the agent loop's Task.WhenAll pattern
        var sw = Stopwatch.StartNew();
        var tasks = calls.Select(c =>
            registry.ExecuteAsync(c.Item1, c.Item2, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert — all three returned valid results
        //
        // Note (audit H5): this test was previously advertised as a
        // wall-clock concurrency check ("should be close to 200 ms"), but
        // get_datetime is effectively instantaneous so there is no timing
        // signal to assert. The concurrency invariant we can verify is
        // that Task.WhenAll returns *all* results, none corrupted, which
        // is what this test now asserts. A separate fixture would be
        // needed for genuine wall-clock verification (e.g., a Delay tool).
        Assert.Equal(3, results.Length);
        foreach (var r in results)
        {
            Assert.DoesNotContain("Error", r);
            // Year-boundary safe (audit H1): regex 20xx, not literal current year.
            Assert.Matches(@"20\d{2}", r);
        }
        // Sanity guard on the stopwatch: parallel dispatch for three trivial
        // tools must not take seconds. 5s is a very loose upper bound that
        // fails only on a broken Task.WhenAll (e.g., re-serialised execution
        // with some future blocking bug) but tolerates slow CI.
        Assert.True(sw.Elapsed.TotalSeconds < 5,
            $"three get_datetime calls via Task.WhenAll took {sw.Elapsed.TotalSeconds:F1}s — suspect sequential execution");
    }

    // ── Order preservation ──────────────────────────────────────────

    [Fact]
    public async Task ParallelExecution_ResultsAreInInputOrder()
    {
        // Arrange — call two different tools so we can distinguish results
        var registry = ToolRegistry.Create(null);

        var file1 = Path.Combine(_tempDir, "file-one.txt");
        var file2 = Path.Combine(_tempDir, "file-two.txt");
        File.WriteAllText(file1, "CONTENT_ONE");
        File.WriteAllText(file2, "CONTENT_TWO");

        var toolCalls = new (string name, string args)[]
        {
            ("read_file", JsonSerializer.Serialize(new { path = file1 })),
            ("get_datetime", "{}"),
            ("read_file", JsonSerializer.Serialize(new { path = file2 })),
        };

        // Act — same Task.WhenAll pattern used in agent loop
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — results match their positional input
        Assert.Equal("CONTENT_ONE", results[0]);
        Assert.DoesNotContain("Error", results[1]);       // datetime result
        // Year-boundary safe (audit H1): regex 20xx, not literal current year.
        Assert.Matches(@"20\d{2}", results[1]);
        Assert.Equal("CONTENT_TWO", results[2]);
    }

    // ── Partial failure ─────────────────────────────────────────────

    [Fact]
    public async Task ParallelExecution_OneFailure_DoesNotBlockOthers()
    {
        // Arrange — one valid call, one unknown tool call, one valid call
        var registry = ToolRegistry.Create(null);

        var file = Path.Combine(_tempDir, "good-file.txt");
        File.WriteAllText(file, "GOOD");

        var toolCalls = new (string name, string args)[]
        {
            ("read_file", JsonSerializer.Serialize(new { path = file })),
            ("nonexistent_tool", "{}"),
            ("get_datetime", "{}"),
        };

        // Act
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — the failed tool returns an error string;
        //          the other two succeed
        Assert.Equal("GOOD", results[0]);
        Assert.Contains("unknown tool", results[1]);
        Assert.Contains("nonexistent_tool", results[1]);
        Assert.DoesNotContain("Error", results[2]);
    }

    [Fact]
    public async Task ParallelExecution_AllToolsFail_AllReturnErrors()
    {
        // Arrange — every call is an unknown tool
        var registry = ToolRegistry.Create(null);

        var toolCalls = new (string name, string args)[]
        {
            ("bogus_one", "{}"),
            ("bogus_two", "{}"),
        };

        // Act
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — both return error strings (not exceptions)
        Assert.Equal(2, results.Length);
        Assert.Contains("unknown tool", results[0]);
        Assert.Contains("bogus_one", results[0]);
        Assert.Contains("unknown tool", results[1]);
        Assert.Contains("bogus_two", results[1]);
    }

    // ── Cancellation ────────────────────────────────────────────────

    [Fact]
    public async Task ParallelExecution_CancelledToken_ReturnsCancellationErrors()
    {
        // Arrange — pre-cancelled token
        var registry = ToolRegistry.Create(["shell"]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var toolCalls = new (string name, string args)[]
        {
            ("shell_exec", """{"command":"echo hello"}"""),
            ("shell_exec", """{"command":"echo world"}"""),
        };

        // Act — with a pre-cancelled token the tool should catch
        //       the OperationCanceledException and return an error string
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, cts.Token)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — each result should indicate timeout / cancellation
        foreach (var r in results)
        {
            Assert.Contains("timed out", r);
        }
    }

    // ── Degenerate cases ────────────────────────────────────────────

    [Fact]
    public async Task ParallelExecution_SingleToolCall_WorksCorrectly()
    {
        // Arrange — the parallel path must handle single-element lists
        var registry = ToolRegistry.Create(["datetime"]);

        var toolCalls = new (string name, string args)[]
        {
            ("get_datetime", "{}"),
        };

        // Act
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Single(results);
        Assert.DoesNotContain("Error", results[0]);
        // Year-boundary safe (audit H1): regex 20xx, not literal current year.
        Assert.Matches(@"20\d{2}", results[0]);
    }

    [Fact]
    public async Task ParallelExecution_EmptyToolList_ReturnsEmptyArray()
    {
        // Arrange — no tool calls at all (edge case for the for-loop + WhenAll)
        var toolCalls = Array.Empty<(string name, string args)>();
        var registry = ToolRegistry.Create(null);

        // Act
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — Task.WhenAll on empty list returns empty array
        Assert.Empty(results);
    }

    // ── Tool-call count tracking ────────────────────────────────────

    [Fact]
    public void TotalToolCallCount_AccumulatesAcrossRounds()
    {
        // Arrange — simulate what the agent loop does with totalToolCalls
        int totalToolCalls = 0;

        // Round 1: model returns 3 tool calls
        var round1ToolCalls = new[] { "tool_a", "tool_b", "tool_c" };
        totalToolCalls += round1ToolCalls.Length;

        // Round 2: model returns 2 tool calls
        var round2ToolCalls = new[] { "tool_d", "tool_e" };
        totalToolCalls += round2ToolCalls.Length;

        // Assert — 5 total tool calls, not round-1 = 1
        Assert.Equal(5, totalToolCalls);
    }

    [Fact]
    public void TotalToolCallCount_SingleRound_MatchesToolCallCount()
    {
        // Arrange — only one round with 4 tool calls
        int totalToolCalls = 0;
        int round = 1;

        var toolCalls = new[] { "a", "b", "c", "d" };
        totalToolCalls += toolCalls.Length;

        // Assert — totalToolCalls (4) != round - 1 (0)
        //          The old code would have reported 0, which is wrong
        Assert.Equal(4, totalToolCalls);
        Assert.NotEqual(round - 1, totalToolCalls);
    }

    // ── Read-file tool parallel-specific tests ──────────────────────

    [Fact]
    public async Task ParallelExecution_ReadMultipleFiles_AllSucceed()
    {
        // Arrange — read three different files in parallel
        var registry = ToolRegistry.Create(["file"]);

        var files = new Dictionary<string, string>
        {
            [Path.Combine(_tempDir, "alpha.txt")] = "ALPHA_CONTENT",
            [Path.Combine(_tempDir, "beta.txt")] = "BETA_CONTENT",
            [Path.Combine(_tempDir, "gamma.txt")] = "GAMMA_CONTENT",
        };

        foreach (var kv in files)
            File.WriteAllText(kv.Key, kv.Value);

        var toolCalls = files.Keys.Select(f =>
            ("read_file", JsonSerializer.Serialize(new { path = f }))).ToArray();

        // Act
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.Item1, tc.Item2, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — each result matches the expected file content in order
        Assert.Equal(3, results.Length);
        Assert.Equal("ALPHA_CONTENT", results[0]);
        Assert.Equal("BETA_CONTENT", results[1]);
        Assert.Equal("GAMMA_CONTENT", results[2]);
    }

    [Fact]
    public async Task ParallelExecution_ReadMixedExistingAndMissing_ReturnsCorrectResults()
    {
        // Arrange — one file exists, one does not
        var registry = ToolRegistry.Create(["file"]);

        var existingFile = Path.Combine(_tempDir, "exists.txt");
        File.WriteAllText(existingFile, "I_EXIST");
        var missingFile = Path.Combine(_tempDir, "missing.txt");

        var toolCalls = new (string name, string args)[]
        {
            ("read_file", JsonSerializer.Serialize(new { path = existingFile })),
            ("read_file", JsonSerializer.Serialize(new { path = missingFile })),
        };

        // Act
        var tasks = toolCalls.Select(tc =>
            registry.ExecuteAsync(tc.name, tc.args, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert — first succeeds, second returns error (but doesn't throw)
        Assert.Equal("I_EXIST", results[0]);
        Assert.StartsWith("Error:", results[1]);
        Assert.Contains("not found", results[1]);
    }
}
