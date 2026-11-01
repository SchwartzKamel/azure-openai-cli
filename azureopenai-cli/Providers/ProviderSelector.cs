using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AzureOpenAI_CLI.Providers;

/// <summary>
/// Metadata entry emitted by <c>--providers</c> and <c>--providers --json</c>.
/// Serialized via <see cref="AppJsonContext"/> (100% Native AOT compatible).
/// </summary>
internal sealed record ProviderDescriptor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("default_endpoint")] string DefaultEndpoint,
    [property: JsonPropertyName("endpoint_env")] string EndpointEnv,
    [property: JsonPropertyName("api_key_env")] string? ApiKeyEnv,
    [property: JsonPropertyName("default_model")] string DefaultModel,
    [property: JsonPropertyName("requires_api_key")] bool RequiresApiKey,
    [property: JsonPropertyName("allows_loopback_http")] bool AllowsLoopbackHttp,
    [property: JsonPropertyName("configured")] bool Configured
);

/// <summary>
/// Resolved provider configuration ready to construct an <see cref="IChatClient"/>.
/// </summary>
internal sealed record ResolvedProviderConfig(
    string Name,
    string Kind,
    Uri EndpointUri,
    string ApiKey,
    string Model,
    bool RequiresApiKey
);

/// <summary>
/// FR-014 / FR-018: Multi-provider selector and <see cref="IChatClient"/> factory.
/// Supports:
/// <list type="bullet">
///   <item><c>azure</c> (default): Azure OpenAI (<c>AZUREOPENAIENDPOINT</c> + <c>AZUREOPENAIAPI</c>)</item>
///   <item><c>openai</c>: OpenAI Platform (<c>https://api.openai.com/v1</c>, <c>OPENAI_API_KEY</c>)</item>
///   <item><c>openrouter</c>: OpenRouter (<c>https://openrouter.ai/api/v1</c>, <c>OPENROUTER_API_KEY</c>)</item>
///   <item><c>ollama</c>: Local Ollama (<c>http://localhost:11434/v1</c>, no key required)</item>
///   <item><c>llamacpp</c> / <c>local</c>: Local llama.cpp server (<c>http://localhost:8080/v1</c>, no key required)</item>
///   <item><c>custom</c>: Any OpenAI-compatible endpoint (<c>AZ_CUSTOM_ENDPOINT</c>, <c>AZ_CUSTOM_API_KEY</c>)</item>
/// </list>
/// Uses the existing <see cref="OpenAIClient"/> from the <c>OpenAI</c> NuGet package
/// and shares <see cref="Program.SharedHttpClient"/> — zero new NuGet dependencies.
/// </summary>
internal static class ProviderSelector
{
    public const string DefaultProvider = "azure";

    public static string NormalizeProviderName(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return DefaultProvider;

        var p = provider.Trim().ToLowerInvariant();
        return p switch
        {
            "azure" or "azure-openai" or "aoai" => "azure",
            "openai" or "oai" => "openai",
            "openrouter" or "or" => "openrouter",
            "ollama" => "ollama",
            "llamacpp" or "llama.cpp" or "local" => "llamacpp",
            "custom" or "openai-compatible" => "custom",
            _ => p,
        };
    }

    public static bool IsSupportedProvider(string? provider)
    {
        var norm = NormalizeProviderName(provider);
        return norm is "azure" or "openai" or "openrouter" or "ollama" or "llamacpp" or "custom";
    }

    public static IReadOnlyList<ProviderDescriptor> DescribeProviders(UserConfig? userConfig = null)
    {
        var azEnd = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT") ?? userConfig?.Endpoint;
        var azKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI") ?? userConfig?.ApiKey;
        var oaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var orKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        var custEnd = Environment.GetEnvironmentVariable("AZ_CUSTOM_ENDPOINT");

        return new[]
        {
            new ProviderDescriptor(
                Name: "azure",
                Kind: "azure-openai",
                DefaultEndpoint: "https://<resource>.openai.azure.com/",
                EndpointEnv: "AZUREOPENAIENDPOINT",
                ApiKeyEnv: "AZUREOPENAIAPI",
                DefaultModel: Program.DefaultModelFallback,
                RequiresApiKey: true,
                AllowsLoopbackHttp: false,
                Configured: !string.IsNullOrWhiteSpace(azEnd) && !string.IsNullOrWhiteSpace(azKey)),
            new ProviderDescriptor(
                Name: "openai",
                Kind: "openai",
                DefaultEndpoint: "https://api.openai.com/v1",
                EndpointEnv: "OPENAI_BASE_URL",
                ApiKeyEnv: "OPENAI_API_KEY",
                DefaultModel: "gpt-4o-mini",
                RequiresApiKey: true,
                AllowsLoopbackHttp: false,
                Configured: !string.IsNullOrWhiteSpace(oaiKey)),
            new ProviderDescriptor(
                Name: "openrouter",
                Kind: "openai-compatible",
                DefaultEndpoint: "https://openrouter.ai/api/v1",
                EndpointEnv: "OPENROUTER_BASE_URL",
                ApiKeyEnv: "OPENROUTER_API_KEY",
                DefaultModel: "openai/gpt-4o-mini",
                RequiresApiKey: true,
                AllowsLoopbackHttp: false,
                Configured: !string.IsNullOrWhiteSpace(orKey)),
            new ProviderDescriptor(
                Name: "ollama",
                Kind: "openai-compatible",
                DefaultEndpoint: "http://localhost:11434/v1",
                EndpointEnv: "OLLAMA_HOST",
                ApiKeyEnv: "OLLAMA_API_KEY",
                DefaultModel: "gemma3",
                RequiresApiKey: false,
                AllowsLoopbackHttp: true,
                Configured: true),
            new ProviderDescriptor(
                Name: "llamacpp",
                Kind: "openai-compatible",
                DefaultEndpoint: "http://localhost:8080/v1",
                EndpointEnv: "LLAMACPP_HOST",
                ApiKeyEnv: "LLAMACPP_API_KEY",
                DefaultModel: "local-model",
                RequiresApiKey: false,
                AllowsLoopbackHttp: true,
                Configured: true),
            new ProviderDescriptor(
                Name: "custom",
                Kind: "openai-compatible",
                DefaultEndpoint: "https://localhost:8000/v1",
                EndpointEnv: "AZ_CUSTOM_ENDPOINT",
                ApiKeyEnv: "AZ_CUSTOM_API_KEY",
                DefaultModel: Program.DefaultModelFallback,
                RequiresApiKey: false,
                AllowsLoopbackHttp: true,
                Configured: !string.IsNullOrWhiteSpace(custEnd)),
        };
    }

    /// <summary>
    /// Resolves endpoint, API key, model, and transport security rules for the
    /// requested provider. Returns <c>(Config, null)</c> on success or
    /// <c>(null, ErrorMessage)</c> on validation failure.
    /// </summary>
    public static (ResolvedProviderConfig? Config, string? ErrorMessage) TryResolve(
        string? providerFlag,
        string? endpointOverride,
        string? apiKeyOverride,
        string? modelFlag,
        UserConfig userConfig)
    {
        var rawProvider = providerFlag
            ?? Environment.GetEnvironmentVariable("AZ_PROVIDER")
            ?? userConfig.DefaultProvider
            ?? DefaultProvider;

        var provider = NormalizeProviderName(rawProvider);
        if (!IsSupportedProvider(provider))
        {
            return (null, $"Unknown provider '{rawProvider}'. Supported: azure, openai, openrouter, ollama, llamacpp, custom.");
        }

        string? endpoint = endpointOverride;
        string? apiKey = apiKeyOverride;
        string defaultModel = Program.DefaultModelFallback;
        bool requiresKey = true;
        bool allowsLoopbackHttp = false;
        string kind = "openai-compatible";

        switch (provider)
        {
            case "azure":
                kind = "azure-openai";
                endpoint ??= Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT") ?? userConfig.Endpoint;
                apiKey ??= Environment.GetEnvironmentVariable("AZUREOPENAIAPI") ?? userConfig.ApiKey;
                defaultModel = Program.DefaultModelFallback;
                requiresKey = true;
                allowsLoopbackHttp = false;
                if (string.IsNullOrWhiteSpace(endpoint))
                    return (null, "AZUREOPENAIENDPOINT environment variable not set. Run 'az-ai --setup' for guided configuration.");
                if (string.IsNullOrWhiteSpace(apiKey))
                    return (null, "AZUREOPENAIAPI environment variable not set. Run 'az-ai --setup' for guided configuration.");
                break;

            case "openai":
                kind = "openai";
                endpoint ??= Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1";
                apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                defaultModel = "gpt-4o-mini";
                requiresKey = true;
                allowsLoopbackHttp = false;
                if (string.IsNullOrWhiteSpace(apiKey))
                    return (null, "OPENAI_API_KEY environment variable not set for provider 'openai'.");
                break;

            case "openrouter":
                kind = "openai-compatible";
                endpoint ??= Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";
                apiKey ??= Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
                defaultModel = "openai/gpt-4o-mini";
                requiresKey = true;
                allowsLoopbackHttp = false;
                if (string.IsNullOrWhiteSpace(apiKey))
                    return (null, "OPENROUTER_API_KEY environment variable not set for provider 'openrouter'.");
                break;

            case "ollama":
                kind = "openai-compatible";
                endpoint ??= NormalizeLocalHostUrl(Environment.GetEnvironmentVariable("OLLAMA_HOST"), "http://localhost:11434/v1");
                apiKey ??= Environment.GetEnvironmentVariable("OLLAMA_API_KEY") ?? "ollama-local";
                defaultModel = "gemma3";
                requiresKey = false;
                allowsLoopbackHttp = true;
                break;

            case "llamacpp":
                kind = "openai-compatible";
                endpoint ??= NormalizeLocalHostUrl(Environment.GetEnvironmentVariable("LLAMACPP_HOST"), "http://localhost:8080/v1");
                apiKey ??= Environment.GetEnvironmentVariable("LLAMACPP_API_KEY") ?? "llamacpp-local";
                defaultModel = "local-model";
                requiresKey = false;
                allowsLoopbackHttp = true;
                break;

            case "custom":
                kind = "openai-compatible";
                endpoint ??= Environment.GetEnvironmentVariable("AZ_CUSTOM_ENDPOINT") ?? userConfig.Endpoint;
                apiKey ??= Environment.GetEnvironmentVariable("AZ_CUSTOM_API_KEY") ?? "custom-local";
                defaultModel = Program.DefaultModelFallback;
                requiresKey = false;
                allowsLoopbackHttp = true;
                if (string.IsNullOrWhiteSpace(endpoint))
                    return (null, "AZ_CUSTOM_ENDPOINT (or --endpoint) is required when using --provider custom.");
                break;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || (endpointUri.Scheme != "https" && endpointUri.Scheme != "http"))
        {
            return (null, $"Invalid endpoint URL: '{endpoint}'. Must be a valid HTTPS URL.");
        }

        // Newman Security Invariant (MEDIUM-002 + FR-018 §5.1):
        // Remote endpoints MUST use https:// so API keys and prompts never cross the network in cleartext.
        // Plaintext http:// is permitted ONLY on loopback (localhost, 127.0.0.1, ::1) for local-capable providers.
        if (endpointUri.Scheme != "https")
        {
            if (!allowsLoopbackHttp || !IsLoopbackHost(endpointUri.Host))
            {
                return (null, $"Invalid endpoint URL: '{endpoint}'. Must be a valid HTTPS URL (plaintext http:// is only permitted on localhost/127.0.0.1/::1 for local providers).");
            }
        }

        var resolvedCliModel = userConfig.ResolveModel(modelFlag);
        var model = resolvedCliModel
            ?? Environment.GetEnvironmentVariable("AZUREOPENAIMODEL")
            ?? userConfig.ResolveSmartDefault()
            ?? defaultModel;

        return (new ResolvedProviderConfig(
            Name: provider,
            Kind: kind,
            EndpointUri: endpointUri,
            ApiKey: apiKey ?? "local",
            Model: model,
            RequiresApiKey: requiresKey), null);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> for the resolved provider configuration,
    /// sharing <see cref="Program.SharedHttpClient"/> for connection pooling.
    /// </summary>
    public static IChatClient CreateChatClient(ResolvedProviderConfig config)
    {
        if (string.Equals(config.Kind, "azure-openai", StringComparison.OrdinalIgnoreCase))
        {
            var azClient = Program.CreateAzureOpenAIClient(config.EndpointUri, config.ApiKey);
            return azClient.GetChatClient(config.Model).AsIChatClient();
        }

        var oaiOptions = new OpenAIClientOptions
        {
            Endpoint = config.EndpointUri,
            Transport = new HttpClientPipelineTransport(Program.SharedHttpClient),
        };
        var oaiClient = new OpenAIClient(new ApiKeyCredential(config.ApiKey), oaiOptions);
        return oaiClient.GetChatClient(config.Model).AsIChatClient();
    }

    internal static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        var trimmed = host.Trim('[', ']');
        if (IPAddress.TryParse(trimmed, out var ip))
        {
            return IPAddress.IsLoopback(ip);
        }

        return false;
    }

    private static string NormalizeLocalHostUrl(string? envValue, string defaultUrl)
    {
        if (string.IsNullOrWhiteSpace(envValue))
            return defaultUrl;

        var trimmed = envValue.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            && !trimmed.EndsWith("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.TrimEnd('/') + "/v1";
        }
        return trimmed;
    }
}
