namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests in the 2.0.1/2.0.2 safety-patch suites mutate process-wide state —
/// env vars (AZURE_TIMEOUT, AZURE_MAX_TOKENS, SYSTEMPROMPT), Console.Error,
/// and shared working directories — while asserting tight error-message
/// strings. Running them in parallel has surfaced races on CI where one
/// test clears AZURE_TIMEOUT mid-assertion of another, and where
/// Console.SetError swaps race with parallel stderr capture.
///
/// xUnit's default is serial-within-class, parallel-across-classes. Pinning
/// the safety-patch triad into a single serialized collection closes the
/// door on cross-class races if parallelism policy ever shifts (e.g. a
/// future xunit.runner.json toggle flipping back to parallel-collections).
///
/// Kept separate from <see cref="TelemetryGlobalStateCollection"/> on
/// purpose — those tests serialize around Observability.Telemetry internals,
/// not env-var / Console.Error state. Merging would over-serialize the
/// telemetry-only suites (3 tests) against the 50+ safety-patch tests for
/// no correctness benefit.
///
/// Members:
///   • <see cref="V201SafetyPatchTests"/>
///   • <see cref="V201ProgramPatchTests"/>
///   • <see cref="V202FollowupPatchTests"/>
///
/// Note: <c>K3DelegateTaskApiKeyLeakTests</c> (in V201ProgramPatchTests.cs)
/// stays in <see cref="TelemetryGlobalStateCollection"/> — it asserts on
/// Telemetry-sink internals, not env-var state.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SafetyPatchCollection
{
    public const string Name = "SafetyPatch";
}
