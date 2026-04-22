using System.Diagnostics;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI_V2.Ralph;

/// <summary>
/// Ralph workflow orchestrator: Plan → Act (agent with tools) → Validate → Retry-if-failed loop.
/// Hand-coded orchestration (MAF v1.1.0 has no Workflows namespace).
/// Honors RALPH_DEPTH recursion cap (3) via DelegateTaskTool.
/// </summary>
internal static class RalphWorkflow
{
    /// <summary>
    /// Runs the Ralph autonomous loop.
    /// Returns 0 on validation pass, 1 on exhaustion/error, 3 on cancellation.
    /// </summary>
    public static async Task<int> RunAsync(
        IChatClient chatClient,
        string taskPrompt,
        string systemPrompt,
        string? validateCommand,
        int maxIterations,
        float temperature,
        int maxTokens,
        int timeoutSeconds,
        string? tools,
        CancellationToken ct = default)
    {
        CheckpointManager.InitializeLog();

        bool showStatus = !Console.IsErrorRedirected;
        if (showStatus)
        {
            Console.Error.WriteLine("🔁 Ralph mode — autonomous loop active");
            if (validateCommand != null)
                Console.Error.WriteLine($"   Validate: {validateCommand}");
            Console.Error.WriteLine($"   Max iterations: {maxIterations}");
            Console.Error.WriteLine();
        }

        // Kramer audit M1: build the agent ONCE — instructions and tools are
        // loop-invariant. Rebuilding every iteration was a v1 port artifact.
        var agent = chatClient.AsAIAgent(
            instructions: systemPrompt + "\n\nYou are in Ralph mode (autonomous loop). " +
                "Complete the task. If there were previous errors, fix them. " +
                "Use tools to read files, run commands, and verify your work.",
            tools: AzureOpenAI_CLI_V2.Tools.ToolRegistry.CreateMafTools(
                tools?.Split(',', StringSplitOptions.RemoveEmptyEntries)));

        // Kramer audit M2: use a single MAF AgentSession ("thread") across all
        // iterations. Prior art string-concatenated the task text into every
        // retry prompt, causing unbounded context growth and token waste. The
        // session carries prior user/assistant turns automatically; retry
        // prompts now only include the *delta* (validation failure + fix ask).
        var session = await agent.CreateSessionAsync(ct);
        var runOpts = new ChatClientAgentRunOptions { ChatOptions = Program.BuildModernChatOptions() };

        // FDR v2 dogfood High-severity (fdr-v2-ralph-exit-code): explicitly
        // track whether validation ever passed AND whether any iteration
        // completed without the agent throwing. Ralph must return non-zero
        // when max-iterations were exhausted without a validation pass, and
        // also when every iteration errored. Prior code relied on fall-through
        // and a bare `return 1` at loop exit; make the contract explicit.
        bool validationPassed = false;
        int successfulIterations = 0;
        int completedIterations = 0;

        // First turn uses the original task text; subsequent turns use short retry feedback.
        string iterationInput = taskPrompt;

        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
            // Phase 5: per-iteration span (no-op when telemetry off)
            using var iterActivity = Observability.Telemetry.ActivitySource.StartActivity(
                "az.ralph.iteration",
                ActivityKind.Internal);
            iterActivity?.SetTag("ralph.iteration", iteration);
            iterActivity?.SetTag("ralph.max_iterations", maxIterations);

            if (showStatus)
                Console.Error.WriteLine($"━━━ Iteration {iteration}/{maxIterations} ━━━");

            // Check for cancellation before each iteration
            if (ct.IsCancellationRequested)
            {
                CheckpointManager.WriteFinalEntry($"Cancelled at iteration {iteration}");
                if (showStatus)
                    Console.Error.WriteLine("\n[cancelled] Ralph loop interrupted");
                return 3;
            }

            // Capture agent output
            var agentOutput = new StringBuilder();
            int agentExitCode = 0;

            try
            {
                using var iterCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                iterCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                await foreach (var update in agent.RunStreamingAsync(
                    iterationInput,
                    session,
                    options: runOpts,
                    cancellationToken: iterCts.Token))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        agentOutput.Append(update.Text);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // User cancelled — save partial state and exit
                CheckpointManager.WriteCheckpoint(iteration, iterationInput, -1,
                    agentOutput.ToString().Trim(), null, null, null);
                CheckpointManager.WriteFinalEntry($"Cancelled mid-iteration {iteration}");
                if (showStatus)
                    Console.Error.WriteLine("\n[cancelled] Ralph loop interrupted");
                return 3;
            }
            catch (OperationCanceledException)
            {
                // Timeout — treat as agent failure
                agentExitCode = 1;
                agentOutput.AppendLine("\n[Agent timed out]");
            }
            catch (Exception ex)
            {
                agentExitCode = 1;
                agentOutput.AppendLine($"\n[Agent error: {ex.Message}]");
            }

            var agentResponse = agentOutput.ToString().Trim();

            if (showStatus && !string.IsNullOrEmpty(agentResponse))
            {
                Console.Error.WriteLine($"📝 Agent response ({agentResponse.Length} chars)");
            }

            // If no validation command, single-pass mode: check agent exit code
            if (validateCommand == null)
            {
                CheckpointManager.WriteCheckpoint(iteration, iterationInput, agentExitCode,
                    agentResponse, null, null, null);

                completedIterations++;
                if (agentExitCode == 0)
                {
                    successfulIterations++;
                    validationPassed = true; // single-pass: agent success IS the verdict
                    if (showStatus)
                        Console.Error.WriteLine($"\n✅ Ralph complete after {iteration} iteration(s)");
                    CheckpointManager.WriteFinalEntry(
                        $"Validation passed at iteration {iteration}/{maxIterations}");
                    Console.Write(agentResponse);
                    if (!string.IsNullOrEmpty(agentResponse) && !agentResponse.EndsWith('\n'))
                        Console.WriteLine();
                    return 0;
                }

                // Agent failed — retry with error-only feedback (no task re-send; session carries it).
                iterationInput = "[Previous attempt failed — please fix the issues and try again.]";
                continue;
            }

            // Run validation command
            if (showStatus)
                Console.Error.Write($"🔍 Validating: {validateCommand}... ");

            var (validationExitCode, validationOutput) = await RunValidationAsync(
                validateCommand, timeoutSeconds, ct);

            CheckpointManager.WriteCheckpoint(iteration, iterationInput, agentExitCode,
                agentResponse, validateCommand, validationExitCode, validationOutput);

            if (agentExitCode == 0) successfulIterations++;
            completedIterations++;

            if (validationExitCode == 0)
            {
                validationPassed = true;
                if (showStatus)
                {
                    Console.Error.WriteLine("✅ PASSED");
                    Console.Error.WriteLine($"\n✅ Ralph complete after {iteration} iteration(s)");
                }
                CheckpointManager.WriteFinalEntry(
                    $"Validation passed at iteration {iteration}/{maxIterations}");
                Console.Write(agentResponse);
                if (!string.IsNullOrEmpty(agentResponse) && !agentResponse.EndsWith('\n'))
                    Console.WriteLine();
                return 0;
            }

            // Validation failed — retry with delta-only feedback. The agent's
            // session already carries the task prompt and prior tool calls, so
            // we send only the new validation output plus a fix request.
            if (showStatus)
                Console.Error.WriteLine($"❌ FAILED (exit {validationExitCode})");

            iterationInput =
                $"[validation failed exit={validationExitCode}]\n" +
                $"{TruncateForPrompt(validationOutput, 4000)}\n" +
                "Fix and retry.";
        }

        // Exhausted iterations — return code contract (fdr-v2-ralph-exit-code):
        //   0 = validation passed (handled above, never reaches here)
        //   1 = max-iterations exhausted without validation pass, or every
        //       iteration errored (agent threw on every attempt)
        //   3 = SIGINT / cancellation (handled above)
        string verdict;
        if (!validationPassed && successfulIterations == 0 && completedIterations > 0)
        {
            verdict = $"Ralph loop errored on all {completedIterations} iteration(s) — agent failed every attempt.";
        }
        else
        {
            verdict = $"Ralph loop exhausted {maxIterations} iterations without passing validation.";
        }
        if (showStatus)
            Console.Error.WriteLine($"\n❌ {verdict}");
        CheckpointManager.WriteFinalEntry(verdict);
        return 1;
    }

    /// <summary>
    /// Runs a shell validation command and returns (exit code, output).
    /// </summary>
    private static async Task<(int exitCode, string output)> RunValidationAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        // Kramer audit L5: use ArgumentList so the shell receives `command` as a
        // single argv entry — no fragile quote-escaping, no shell-injection
        // surprises when the command itself contains quotes or `$`. Matches
        // the safer pattern already used by ShellExecTool.
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start validation process");

            process.StandardInput.Close();

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var output = stdout;
            if (!string.IsNullOrEmpty(stderr))
                output += $"\n[stderr]\n{stderr}";

            return (process.ExitCode, output);
        }
        catch (OperationCanceledException)
        {
            return (1, "Validation timed out");
        }
    }

    private static string TruncateForPrompt(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "(empty)";

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "\n... (truncated)";
    }
}
