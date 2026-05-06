namespace AzureOpenAI_CLI.Capabilities;

// S03E18 -- The Capability Gate.
//
// CapabilityDescriptor is a flat record of feature flags for a (preset, model)
// pair. It is the single source of truth queried at dispatch time so a request
// that the downstream model cannot honour fails fast with a friendly error
// instead of a confusing 4xx wire response from the provider. AOT-safe: no
// reflection-based serialization, no DI, no dynamic dispatch.

/// <summary>
/// Capability snapshot for one provider+model. <c>ToolCalls</c> covers the
/// OpenAI-style <c>tools</c> array; <c>Streaming</c> covers SSE/chunked
/// streaming; <c>Vision</c> covers multimodal image input; <c>JsonMode</c>
/// covers <c>response_format=json_object</c> / structured-output schemas.
/// <c>MaxContextTokens</c> is informational (null when unknown).
/// </summary>
public sealed record CapabilityDescriptor(
    bool ToolCalls,
    bool Streaming,
    bool Vision,
    bool JsonMode,
    int? MaxContextTokens)
{
    /// <summary>
    /// Conservative fallback for unknown presets / models: every capability
    /// disabled, context length unknown. Mirrors the "if you do not know,
    /// say no" doctrine -- a confused 500 is worse than a refusal the user
    /// can override.
    /// </summary>
    public static CapabilityDescriptor Conservative()
        => new(ToolCalls: false, Streaming: false, Vision: false, JsonMode: false, MaxContextTokens: null);

    /// <summary>
    /// Permissive default for caller-owned deployments (Azure OpenAI,
    /// Foundry): the user picked the deployment, so we trust they know
    /// whether it speaks tools / vision / json. Every flag true; context
    /// length still unknown because deployment metadata is not introspected.
    /// </summary>
    public static CapabilityDescriptor Permissive()
        => new(ToolCalls: true, Streaming: true, Vision: true, JsonMode: true, MaxContextTokens: null);
}
