using Xunit;

namespace AzureOpenAI_CLI_V2.Tests;

/// <summary>
/// Collection definition for tests that capture Console.Out/Err.
/// Sequential execution prevents interleaved console output in parallel test runs.
/// </summary>
[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
public class ConsoleCaptureCollection
{
}
