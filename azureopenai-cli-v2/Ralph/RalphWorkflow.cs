using System.Diagnostics;
using System.Text;
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

        string currentPrompt = taskPrompt;

        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
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

            // Build agent for this iteration
            var agent = chatClient.AsAIAgent(
                instructions: systemPrompt + "\n\nYou are in Ralph mode (autonomous loop). " +
                    "Complete the task. If there were previous errors, fix them. " +
                    "Use tools to read files, run commands, and verify your work.",
                tools: AzureOpenAI_CLI_V2.Tools.ToolRegistry.CreateMafTools(
                    tools?.Split(',', StringSplitOptions.RemoveEmptyEntries)));

            // Capture agent output
            var agentOutput = new StringBuilder();
            int agentExitCode = 0;

            try
            {
                using var iterCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                iterCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                await foreach (var update in agent.RunStreamingAsync(currentPrompt, cancellationToken: iterCts.Token))
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
                CheckpointManager.WriteCheckpoint(iteration, currentPrompt, -1,
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
                CheckpointManager.WriteCheckpoint(iteration, currentPrompt, agentExitCode,
                    agentResponse, null, null, null);

                if (agentExitCode == 0)
                {
                    if (showStatus)
                        Console.Error.WriteLine($"\n✅ Ralph complete after {iteration} iteration(s)");
                    Console.Write(agentResponse);
                    if (!string.IsNullOrEmpty(agentResponse) && !agentResponse.EndsWith('\n'))
                        Console.WriteLine();
                    return 0;
                }

                // Agent failed — retry with error context
                currentPrompt = $"{taskPrompt}\n\n[Previous attempt failed]\n" +
                    $"[Agent response]: {agentResponse}\n\n" +
                    "Please fix the issues and try again.";
                continue;
            }

            // Run validation command
            if (showStatus)
                Console.Error.Write($"🔍 Validating: {validateCommand}... ");

            var (validationExitCode, validationOutput) = await RunValidationAsync(
                validateCommand, timeoutSeconds, ct);

            CheckpointManager.WriteCheckpoint(iteration, currentPrompt, agentExitCode,
                agentResponse, validateCommand, validationExitCode, validationOutput);

            if (validationExitCode == 0)
            {
                if (showStatus)
                {
                    Console.Error.WriteLine("✅ PASSED");
                    Console.Error.WriteLine($"\n✅ Ralph complete after {iteration} iteration(s)");
                }
                Console.Write(agentResponse);
                if (!string.IsNullOrEmpty(agentResponse) && !agentResponse.EndsWith('\n'))
                    Console.WriteLine();
                return 0;
            }

            // Validation failed — retry with feedback
            if (showStatus)
                Console.Error.WriteLine($"❌ FAILED (exit {validationExitCode})");

            currentPrompt = $"{taskPrompt}\n\n" +
                $"[Iteration {iteration} — validation FAILED]\n" +
                $"[Validation command: {validateCommand}]\n" +
                $"[Exit code: {validationExitCode}]\n" +
                $"[Validation output]:\n{TruncateForPrompt(validationOutput, 4000)}\n\n" +
                $"[Previous agent response]:\n{TruncateForPrompt(agentResponse, 2000)}\n\n" +
                "Fix the issues shown in the validation output. Use tools to read and modify files as needed.";
        }

        // Exhausted iterations
        var exhaustedMsg = $"Ralph loop exhausted {maxIterations} iterations without passing validation.";
        if (showStatus)
            Console.Error.WriteLine($"\n❌ {exhaustedMsg}");
        CheckpointManager.WriteFinalEntry(exhaustedMsg);
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

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

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
