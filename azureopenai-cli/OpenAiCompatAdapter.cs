using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AzureOpenAI_CLI;

// S03E09 -- The Compat. ADR-010 deliverable.
//
// OpenAiCompatAdapter is a single seam that constructs an IChatClient against
// any OpenAI-wire-compatible /v1/chat/completions endpoint. Each provider --
// OpenAI direct, Groq, Together, Cloudflare Workers AI, and (Arc 3) Ollama,
// llama.cpp, NIM, vLLM -- shows up as an OpenAiCompatPreset, NOT as its own
// adapter class. ADR-010 explicitly forbids per-vendor adapters for any
// provider that speaks the OpenAI wire protocol.
//
// AOT: this file does not introduce any reflection-based serialization. Every
// preset is constructed in-process; no JSON contract is exposed.

/// <summary>
/// Configuration preset for an OpenAI-wire-compatible endpoint. Captures only
/// the fields needed to build an HTTP-level client: base URL, the env var that
/// supplies the API key (so the adapter never holds a secret in a static
/// dictionary), and an optional org-id header for OpenAI-direct multi-org
/// accounts. <c>AuthScheme</c> is the auth-header prefix (default
/// <c>Bearer</c>); kept as a knob so future presets that use a non-Bearer
/// scheme do not require a new policy class.
/// </summary>
internal sealed record OpenAiCompatPreset(
    string Name,
    Uri BaseUrl,
    string ApiKeyEnvVar,
    string? OrgEnvVar = null,
    string AuthScheme = "Bearer");

/// <summary>
/// ADR-010 OpenAI-compatible provider adapter. Builds an <see cref="IChatClient"/>
/// from a preset + model name. Reads the API key from the preset's env var at
/// build time -- credentials never live in code or in the preset record.
///
/// Precedence (mirrors S03E09 wiring in <c>Program.BuildChatClient</c>):
///   1. Azure Foundry allowlist (<c>AZURE_FOUNDRY_MODELS</c>) wins.
///   2. OpenAI-compat allowlist (<c>AZ_AI_COMPAT_MODELS</c>) routes to a
///      named preset.
///   3. Default: Azure OpenAI path.
/// </summary>
internal static class OpenAiCompatAdapter
{
    /// <summary>
    /// Built-in presets (case-insensitive lookup). Cloudflare's preset uses a
    /// placeholder URL containing <c>{account_id}</c> -- callers MUST set
    /// <c>CLOUDFLARE_ACCOUNT_ID</c> and the URL is rewritten at build time.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, OpenAiCompatPreset> BuiltIn =
        new Dictionary<string, OpenAiCompatPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = new(
                "openai",
                new Uri("https://api.openai.com/v1"),
                "OPENAI_API_KEY",
                OrgEnvVar: "OPENAI_ORG_ID"),
            ["groq"] = new(
                "groq",
                new Uri("https://api.groq.com/openai/v1"),
                "GROQ_API_KEY"),
            ["together"] = new(
                "together",
                new Uri("https://api.together.xyz/v1"),
                "TOGETHER_API_KEY"),
            // Cloudflare Workers AI: needs-account-id. Placeholder URL is
            // rewritten by Build() once CLOUDFLARE_ACCOUNT_ID is read. Keeping
            // the preset listed (instead of throwing on registration) lets
            // tests and `--config show` enumerate it without exporting the
            // account id.
            ["cloudflare"] = new(
                "cloudflare",
                new Uri("https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1"),
                "CLOUDFLARE_API_TOKEN"),
        };

    /// <summary>
    /// Resolve a preset by name (case-insensitive). Returns null if unknown --
    /// callers decide whether that is fatal. Use <see cref="ResolveOrThrow"/>
    /// when an unknown preset must surface as an actionable error message.
    /// </summary>
    internal static OpenAiCompatPreset? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return BuiltIn.TryGetValue(name.Trim(), out var preset) ? preset : null;
    }

    /// <summary>
    /// Resolve a preset or throw <see cref="ArgumentException"/> with an
    /// actionable message listing every known preset. Used by the dispatch
    /// path so a typo in <c>AZ_AI_COMPAT_MODELS</c> surfaces with a hint
    /// instead of a silent fall-through to the Azure default.
    /// </summary>
    internal static OpenAiCompatPreset ResolveOrThrow(string name)
    {
        var preset = Resolve(name);
        if (preset is not null) return preset;

        var known = string.Join(", ", BuiltIn.Keys.OrderBy(k => k, StringComparer.Ordinal));
        throw new ArgumentException(
            $"Unknown OpenAI-compatible preset '{name}'. Known presets: {known}. "
            + "Set AZ_AI_COMPAT_MODELS=<preset>:<model>[,...] using one of these names.",
            nameof(name));
    }

    /// <summary>
    /// Parse <c>AZ_AI_COMPAT_MODELS</c> as a comma-separated list of
    /// <c>preset:model</c> pairs. Returns a dictionary mapping each model name
    /// (case-insensitive) to its preset name. Whitespace around items, around
    /// the colon, and empty entries are tolerated. Malformed entries (no
    /// colon, empty preset, or empty model) throw
    /// <see cref="ArgumentException"/>. Returns null if the env var is unset
    /// or empty.
    /// </summary>
    internal static Dictionary<string, string>? ParseCompatModels(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0 || colon == entry.Length - 1)
            {
                throw new ArgumentException(
                    $"Malformed AZ_AI_COMPAT_MODELS entry '{entry}'. "
                    + "Expected '<preset>:<model>' (e.g. 'openai:gpt-4o-mini').",
                    nameof(raw));
            }

            var preset = entry.Substring(0, colon).Trim();
            var model = entry.Substring(colon + 1).Trim();
            if (preset.Length == 0 || model.Length == 0)
            {
                throw new ArgumentException(
                    $"Malformed AZ_AI_COMPAT_MODELS entry '{entry}'. "
                    + "Both preset and model must be non-empty.",
                    nameof(raw));
            }

            map[model] = preset;
        }
        return map.Count > 0 ? map : null;
    }

    /// <summary>
    /// Reads <c>AZ_AI_COMPAT_MODELS</c> from the process environment and
    /// returns the parsed model -> preset map (or null if unset / empty).
    /// </summary>
    internal static Dictionary<string, string>? ParseCompatModelsFromEnv()
        => ParseCompatModels(Environment.GetEnvironmentVariable("AZ_AI_COMPAT_MODELS"));

    /// <summary>
    /// Build an <see cref="IChatClient"/> for a given model + preset. Reads the
    /// API key (and optional org id) from the env vars named on the preset.
    /// The optional <paramref name="http"/> parameter is reserved for tests /
    /// future custom transports -- it is currently ignored because the OpenAI
    /// SDK does not accept an external <c>HttpClient</c> directly. Throws
    /// <see cref="InvalidOperationException"/> if the API key env var is unset
    /// or if Cloudflare's preset is selected without
    /// <c>CLOUDFLARE_ACCOUNT_ID</c>.
    /// </summary>
    internal static IChatClient Build(string modelOrAlias, OpenAiCompatPreset preset, HttpClient? http = null)
    {
        if (string.IsNullOrWhiteSpace(modelOrAlias))
            throw new ArgumentException("Model must be non-empty.", nameof(modelOrAlias));

        var apiKey = Environment.GetEnvironmentVariable(preset.ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible preset '{preset.Name}' requires env var "
                + $"'{preset.ApiKeyEnvVar}' but it is unset or empty.");
        }

        // Cloudflare Workers AI: rewrite the {account_id} placeholder.
        var endpoint = preset.BaseUrl;
        if (endpoint.OriginalString.Contains("{account_id}", StringComparison.Ordinal))
        {
            var accountId = Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID");
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new InvalidOperationException(
                    "Cloudflare preset requires env var 'CLOUDFLARE_ACCOUNT_ID' "
                    + "to substitute into the endpoint URL.");
            }
            endpoint = new Uri(endpoint.OriginalString.Replace("{account_id}", accountId.Trim(), StringComparison.Ordinal));
        }

        // SECURITY: HTTPS-only for non-loopback hosts. Mirrors BuildChatClient.
        if (endpoint.Scheme != "https" && !IsLoopback(endpoint))
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible endpoint '{endpoint}' must be HTTPS unless loopback.");
        }

        var options = new OpenAIClientOptions { Endpoint = endpoint };

        // Default Bearer auth is what the OpenAI SDK already emits via
        // ApiKeyCredential -- no policy needed. For non-Bearer schemes (future
        // presets) we install a small replace-Authorization policy. Same shape
        // as FoundryAuthPolicy but stays inside this file to keep the seam
        // self-contained.
        if (!string.Equals(preset.AuthScheme, "Bearer", StringComparison.Ordinal))
        {
            options.AddPolicy(new CompatAuthPolicy(apiKey, preset.AuthScheme), PipelinePosition.PerCall);
        }

        // Optional OpenAI-Organization header (OPENAI_ORG_ID). Only OpenAI
        // direct uses this; other presets leave OrgEnvVar null.
        if (!string.IsNullOrWhiteSpace(preset.OrgEnvVar))
        {
            var org = Environment.GetEnvironmentVariable(preset.OrgEnvVar);
            if (!string.IsNullOrWhiteSpace(org))
            {
                options.AddPolicy(new OrgHeaderPolicy(org), PipelinePosition.PerCall);
            }
        }

        return new ChatClient(modelOrAlias, new ApiKeyCredential(apiKey), options).AsIChatClient();
    }

    private static bool IsLoopback(Uri uri)
    {
        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host == "127.0.0.1" || host == "::1" || host == "[::1]") return true;
        return false;
    }

    /// <summary>
    /// Replaces the SDK's <c>Authorization: Bearer</c> with a custom scheme
    /// (e.g. <c>Token</c>, <c>ApiKey</c>). Reserved for future presets; not
    /// exercised by any built-in preset today.
    /// </summary>
    internal sealed class CompatAuthPolicy : PipelinePolicy
    {
        private readonly string _apiKey;
        private readonly string _scheme;

        internal CompatAuthPolicy(string apiKey, string scheme)
        {
            _apiKey = apiKey;
            _scheme = scheme;
        }

        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }

        private void Apply(PipelineMessage message)
        {
            message.Request.Headers.Set("Authorization", _scheme + " " + _apiKey);
        }
    }

    /// <summary>Adds the <c>OpenAI-Organization</c> header when set.</summary>
    internal sealed class OrgHeaderPolicy : PipelinePolicy
    {
        private readonly string _org;
        internal OrgHeaderPolicy(string org) { _org = org; }

        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            message.Request.Headers.Set("OpenAI-Organization", _org);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            message.Request.Headers.Set("OpenAI-Organization", _org);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }
    }
}
