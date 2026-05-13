using AzureOpenAI_CLI.Registry;

namespace AzureOpenAI_CLI.Resolution;

// S04E05 -- The Picker (Costanza lead, Maestro co-lead).
//
// Pure, deterministic, AOT-safe default-model resolver. Four reason codes:
// EXPLICIT, PREFER_AXIS, ALLOWLIST_HEAD, FALLBACK. The function reads only
// its arguments: no env-var reads, no clock reads, no static lookups, no
// I/O, no logging. Determinism is asserted in tests over 100 iterations.
//
// Wired into Program.cs at the single AZUREOPENAIMODEL[0] resolution site;
// the resolved ResolutionResult.Model continues into the existing flow,
// and the CapabilityGate (E03) runs after this picker by design -- the
// picker is gate-blind.
//
// E09 (--prefer flag) will wire ResolverInputs.PreferAxis from the CLI;
// E05 lands the code path and tests it via ResolverInputs.

/// <summary>
/// Minimal registry surface consumed by the picker. An adapter over
/// <see cref="ModelRegistry"/> lives in <see cref="ArrayModelRegistry"/>;
/// tests pass an in-memory implementation directly.
/// </summary>
internal interface IModelRegistry
{
    bool TryGet(string name, out ModelRegistryEntry? entry);
}

/// <summary>
/// Array-backed adapter so Program.cs can wrap its loaded
/// <see cref="ModelRegistryEntry"/>[] without dragging the static
/// <c>ModelRegistry</c> into a pure resolver.
/// </summary>
internal sealed class ArrayModelRegistry : IModelRegistry
{
    private readonly ModelRegistryEntry[] _entries;

    public ArrayModelRegistry(ModelRegistryEntry[] entries)
    {
        _entries = entries ?? Array.Empty<ModelRegistryEntry>();
    }

    public bool TryGet(string name, out ModelRegistryEntry? entry)
    {
        if (string.IsNullOrEmpty(name)) { entry = null; return false; }
        for (int i = 0; i < _entries.Length; i++)
        {
            if (string.Equals(_entries[i].Name, name, StringComparison.Ordinal))
            {
                entry = _entries[i];
                return true;
            }
        }
        entry = null;
        return false;
    }
}

/// <summary>
/// Inputs to <see cref="ResolveSmartDefault.Pick"/>. All fields are
/// parameters; the picker never reads env vars or globals.
/// <c>ExplicitModel</c> short-circuits all other steps when non-empty.
/// <c>PreferAxis</c> selects a ranking axis when set; valid values are
/// <c>cost</c>, <c>latency</c>, <c>quality</c> (ordinal, case-sensitive).
/// Any other non-null value is treated as if no axis were supplied; the
/// fact is surfaced in <see cref="ResolutionResult.HumanReason"/>.
/// <c>Allowlist</c> is the ordered comma-separated set from
/// <c>AZUREOPENAIMODEL</c>; empty triggers FALLBACK.
/// </summary>
internal sealed record ResolverInputs(
    string? ExplicitModel,
    string? PreferAxis,
    string[] Allowlist);

/// <summary>
/// Result of the picker. Three fields: the resolved model name (empty on
/// FALLBACK with no usable entry), the reason code (one of
/// <see cref="ResolutionReason"/>), and a human-readable explanation
/// capped at 120 ASCII characters.
/// </summary>
internal sealed record ResolutionResult(
    string Model,
    string ReasonCode,
    string HumanReason);

/// <summary>
/// String constants for the four locked reason codes. Defined as
/// <c>public const string</c> -- no enum (AOT JSON cost) and no scattered
/// string literals in the codebase.
/// </summary>
internal static class ResolutionReason
{
    public const string EXPLICIT = "EXPLICIT";
    public const string PREFER_AXIS = "PREFER_AXIS";
    public const string ALLOWLIST_HEAD = "ALLOWLIST_HEAD";
    public const string FALLBACK = "FALLBACK";
}

/// <summary>
/// Pure-function default model picker. See file-level comment for the
/// contract; <see cref="Pick"/> is the only entry point.
/// </summary>
internal static class ResolveSmartDefault
{
    // 120-char HumanReason cap (brief AC #11). All formatted strings below
    // stay inside this budget; runner-up names are truncated only if the
    // total would otherwise overflow -- see TruncateForCap.
    private const int HumanReasonCap = 120;

    /// <summary>
    /// Resolve the default model. Pure function: no I/O, no statics,
    /// no clock, no env reads. Deterministic for any given input.
    /// </summary>
    public static ResolutionResult Pick(IModelRegistry registry, ResolverInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inputs);

        // Step 1: EXPLICIT -- short-circuits axis and allowlist. Whitespace-
        // only treated as null (CLI parsers leak " " sometimes; we do not
        // want to force a FALLBACK on that).
        var explicitModel = NormalizeOrNull(inputs.ExplicitModel);
        if (explicitModel is not null)
        {
            return new ResolutionResult(
                explicitModel,
                ResolutionReason.EXPLICIT,
                Cap("model '" + explicitModel + "' chosen because user passed --model"));
        }

        var allowlist = inputs.Allowlist ?? Array.Empty<string>();

        // Step 4 (early): empty allowlist -> FALLBACK. Resolver does not
        // exit; Program.cs decides what to do with the empty model.
        if (allowlist.Length == 0)
        {
            return new ResolutionResult(
                "",
                ResolutionReason.FALLBACK,
                Cap("AZUREOPENAIMODEL is empty; set the env-var to a comma-separated list of deployment names"));
        }

        // Step 2: PREFER_AXIS. Unknown axis values fall through to step 3
        // and surface the ignored axis name in the ALLOWLIST_HEAD reason.
        var axis = NormalizeOrNull(inputs.PreferAxis);
        if (axis is not null && IsKnownAxis(axis))
        {
            var ranked = RankByAxis(registry, allowlist, axis);
            if (ranked.Length > 0)
            {
                var winner = ranked[0];
                var runnerUp = ranked.Length > 1 ? ranked[1].Name : null;
                var tier = winner.Tier ?? "unknown";
                var human = runnerUp is null
                    ? "model '" + winner.Name + "' chosen for axis '" + axis + "' (tier " + tier + ")"
                    : "model '" + winner.Name + "' chosen for axis '" + axis + "' (tier " + tier + "; runner-up " + runnerUp + ")";
                return new ResolutionResult(
                    winner.Name,
                    ResolutionReason.PREFER_AXIS,
                    Cap(human));
            }
            // Axis set but allowlist has no entries the registry knows
            // about. Fall through to ALLOWLIST_HEAD / FALLBACK so the user
            // still gets a deterministic answer; the gate names the bad
            // pick downstream.
        }

        // Step 3: ALLOWLIST_HEAD. Today's behavior, now explainable.
        var head = allowlist[0];
        if (registry.TryGet(head, out _))
        {
            string human;
            if (axis is not null && !IsKnownAxis(axis))
            {
                human = "model '" + head + "' chosen as head of AZUREOPENAIMODEL (unknown axis '" + axis + "' ignored)";
            }
            else
            {
                human = "model '" + head + "' chosen as head of AZUREOPENAIMODEL";
            }
            return new ResolutionResult(
                head,
                ResolutionReason.ALLOWLIST_HEAD,
                Cap(human));
        }

        // Step 4 (late): head not in registry -> FALLBACK. We still
        // return the head as the model so existing flows continue to
        // surface a useful error naming the resolved deployment.
        return new ResolutionResult(
            head,
            ResolutionReason.FALLBACK,
            Cap("allowlist head '" + head + "' not in registry; see --doctor or fix AZUREOPENAIMODEL"));
    }

    private static string? NormalizeOrNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IsKnownAxis(string axis)
        => string.Equals(axis, "cost", StringComparison.Ordinal)
        || string.Equals(axis, "latency", StringComparison.Ordinal)
        || string.Equals(axis, "quality", StringComparison.Ordinal);

    // Returned entries are paired with the tier value used to rank them
    // so the HumanReason can quote it without re-deriving.
    private readonly struct RankedEntry
    {
        public readonly string Name;
        public readonly string? Tier;
        public RankedEntry(string name, string? tier) { Name = name; Tier = tier; }
    }

    private static RankedEntry[] RankByAxis(
        IModelRegistry registry, string[] allowlist, string axis)
    {
        // Stable sort: collect (name, allowlistIndex, rank, tier) then
        // sort by (rank ASC, allowlistIndex ASC). Missing tier -> rank
        // int.MaxValue so it sorts last. We do not consult the registry
        // for entries the allowlist names but the registry does not know
        // -- those still participate with rank int.MaxValue, keeping the
        // picker gate-blind and avoiding accidental filtering.
        var scratch = new (string Name, int Index, int Rank, string? Tier)[allowlist.Length];
        for (int i = 0; i < allowlist.Length; i++)
        {
            var name = allowlist[i];
            string? tier = null;
            if (registry.TryGet(name, out var entry) && entry is not null)
            {
                tier = SelectTier(entry, axis);
            }
            scratch[i] = (name, i, RankTier(axis, tier), tier);
        }

        // Insertion sort -- O(n^2) but n is the comma-separated AZUREOPENAIMODEL
        // length (typically 1-5). Avoids LINQ allocation and is stable by
        // construction.
        for (int i = 1; i < scratch.Length; i++)
        {
            var cur = scratch[i];
            int j = i - 1;
            while (j >= 0 && Compare(scratch[j], cur) > 0)
            {
                scratch[j + 1] = scratch[j];
                j--;
            }
            scratch[j + 1] = cur;
        }

        var result = new RankedEntry[scratch.Length];
        for (int i = 0; i < scratch.Length; i++)
        {
            result[i] = new RankedEntry(scratch[i].Name, scratch[i].Tier);
        }
        return result;
    }

    private static int Compare(
        (string Name, int Index, int Rank, string? Tier) a,
        (string Name, int Index, int Rank, string? Tier) b)
    {
        if (a.Rank != b.Rank) return a.Rank - b.Rank;
        return a.Index - b.Index;
    }

    private static string? SelectTier(ModelRegistryEntry entry, string axis)
    {
        if (string.Equals(axis, "cost", StringComparison.Ordinal))
        {
            // CostTier is non-nullable string on the record; treat
            // empty/"unknown" as missing so unknown sorts last.
            var v = entry.CostTier;
            if (string.IsNullOrEmpty(v)
                || string.Equals(v, "unknown", StringComparison.Ordinal))
            {
                return null;
            }
            return v;
        }
        if (string.Equals(axis, "latency", StringComparison.Ordinal))
        {
            return entry.LatencyTier;
        }
        // quality
        return entry.QualityTier;
    }

    // Per-axis tier rank. Lower = better (sorts first). Missing -> last.
    private static int RankTier(string axis, string? tier)
    {
        if (string.IsNullOrEmpty(tier)) return int.MaxValue;
        if (string.Equals(axis, "cost", StringComparison.Ordinal))
        {
            if (string.Equals(tier, "low", StringComparison.Ordinal)) return 0;
            if (string.Equals(tier, "medium", StringComparison.Ordinal)) return 1;
            if (string.Equals(tier, "high", StringComparison.Ordinal)) return 2;
            return int.MaxValue;
        }
        if (string.Equals(axis, "latency", StringComparison.Ordinal))
        {
            if (string.Equals(tier, "fast", StringComparison.Ordinal)) return 0;
            if (string.Equals(tier, "medium", StringComparison.Ordinal)) return 1;
            if (string.Equals(tier, "slow", StringComparison.Ordinal)) return 2;
            return int.MaxValue;
        }
        // quality: premium > standard > basic, so premium ranks 0.
        if (string.Equals(tier, "premium", StringComparison.Ordinal)) return 0;
        if (string.Equals(tier, "standard", StringComparison.Ordinal)) return 1;
        if (string.Equals(tier, "basic", StringComparison.Ordinal)) return 2;
        return int.MaxValue;
    }

    private static string Cap(string s)
        => s.Length <= HumanReasonCap ? s : s.Substring(0, HumanReasonCap);
}
