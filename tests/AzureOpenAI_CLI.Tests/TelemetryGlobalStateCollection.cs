namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests that mutate global <c>AzureOpenAI_CLI.Observability.Telemetry</c>
/// state (StderrWriter, Initialize/Shutdown, metrics pipeline) must join this
/// collection so xUnit serializes them. Running them in parallel races:
/// one test's Shutdown() can tear down the sink another test is still
/// asserting against, surfacing as "expected a cost event on stderr" when the
/// writer was swapped back to Console.Error mid-emission.
///
/// Reproduced reliably with ~20% rate under CI parallelism
/// (ubuntu-latest, run 24700316040). Owners: DelegateTaskToolTests,
/// ObservabilityTests. Add any new Telemetry-touching suites here.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryGlobalStateCollection
{
    public const string Name = "TelemetryGlobalState";
}
