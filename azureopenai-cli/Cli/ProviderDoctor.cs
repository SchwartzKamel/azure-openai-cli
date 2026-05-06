using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AzureOpenAI_CLI.Cli;

// S03E15 -- The Probe. Costanza's diagnostic subcommand.
//
// `az-ai --doctor` (also exposed as the "providers doctor" sub-command)
// probes every configured provider and reports health WITHOUT issuing any
// authenticated API call and WITHOUT emitting credential values. The check
// surface is intentionally narrow:
//
//   * endpoint host DNS resolution (3s cap, parallel via Task.WhenAll)
//   * credential env-var presence (boolean only -- never the value)
//   * model-allowlist presence (count only)
//
// Every textual line that lands on stdout / stderr is routed through
// SecretRedactor.Redact as defense-in-depth -- even though the doctor
// constructs only safe, structural strings, this guarantees that any
// future regression that accidentally interpolates a key still gets
// scrubbed before the user ever sees it.
//
// AOT-safe: BCL-only, no reflection. JSON output uses AppJsonContext.

/// <summary>
/// Diagnostic probe for every configured provider. See file header for
/// surface and policy. Public test seam: <see cref="DnsResolver"/>.
/// </summary>
internal static class ProviderDoctor
{
    /// <summary>Per-provider DNS timeout cap.</summary>
    internal static TimeSpan DnsTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Test seam. Production wires <see cref="DefaultDnsResolveAsync"/>
    /// which wraps <see cref="Dns.GetHostEntryAsync(string)"/> with a
    /// timeout. Returns true on resolve, false on failure / timeout.
    /// </summary>
    internal static Func<string, CancellationToken, Task<bool>> DnsResolver { get; set; }
        = DefaultDnsResolveAsync;

    /// <summary>Default production DNS resolver.</summary>
    internal static async Task<bool> DefaultDnsResolveAsync(string host, CancellationToken ct)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(host, ct).ConfigureAwait(false);
            return entry?.AddressList?.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Run the doctor. Returns 0 when every configured provider is healthy,
    /// 1 when at least one is unhealthy. With no providers configured the
    /// exit code is 0 and a helpful message is printed.
    /// </summary>
    public static int Run(bool jsonMode, bool plain, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var providers = CollectProviders();
        var entries = ProbeAllAsync(providers).GetAwaiter().GetResult();

        var allHealthy = entries.All(e => e.Healthy);
        var report = new ProviderDoctorReport(entries, allHealthy);

        if (jsonMode)
        {
            var json = JsonSerializer.Serialize(report, AppJsonContext.Default.ProviderDoctorReport);
            stdout.WriteLine(SecretRedactor.Redact(json));
            return entries.Count == 0 ? 0 : (allHealthy ? 0 : 1);
        }

        if (entries.Count == 0)
        {
            stdout.WriteLine(SecretRedactor.Redact(
                "az-ai providers doctor: no providers configured."));
            stdout.WriteLine(SecretRedactor.Redact(
                "  Set AZUREOPENAIENDPOINT, AZURE_FOUNDRY_ENDPOINT, or AZ_AI_COMPAT_MODELS,"));
            stdout.WriteLine(SecretRedactor.Redact(
                "  or run 'az-ai --setup' for guided configuration."));
            return 0;
        }

        if (plain)
        {
            WritePlain(entries, stdout);
        }
        else
        {
            WriteTable(entries, stdout);
        }

        return allHealthy ? 0 : 1;
    }

    // -- Provider enumeration--------------------------------------------

    /// <summary>
    /// Build the input list from the process environment. Compat presets
    /// are de-duplicated by preset name (one row per preset, even when the
    /// allowlist has multiple <c>preset:model</c> entries for it).
    /// </summary>
    internal static List<ProviderProbe> CollectProviders()
    {
        var result = new List<ProviderProbe>();

        var azureEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        if (!string.IsNullOrWhiteSpace(azureEndpoint))
        {
            result.Add(new ProviderProbe(
                Name: "azure",
                Endpoint: azureEndpoint!.Trim(),
                CredEnvVar: "AZUREOPENAIAPI",
                ModelsCount: CountModels(Environment.GetEnvironmentVariable("AZUREOPENAIMODEL"))));
        }

        var foundryEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(foundryEndpoint))
        {
            result.Add(new ProviderProbe(
                Name: "foundry",
                Endpoint: foundryEndpoint!.Trim(),
                CredEnvVar: "AZURE_FOUNDRY_KEY",
                ModelsCount: CountModels(Environment.GetEnvironmentVariable("AZURE_FOUNDRY_MODELS"))));
        }

        // Compat presets -- one row per preset referenced by AZ_AI_COMPAT_MODELS.
        Dictionary<string, string>? compatModels = null;
        try
        {
            compatModels = OpenAiCompatAdapter.ParseCompatModelsFromEnv();
        }
        catch (ArgumentException)
        {
            // Malformed AZ_AI_COMPAT_MODELS: surface as a synthetic
            // unhealthy preset so the doctor reports the misconfig
            // without aborting the whole run.
            result.Add(new ProviderProbe(
                Name: "compat:malformed",
                Endpoint: "(unparseable AZ_AI_COMPAT_MODELS)",
                CredEnvVar: string.Empty,
                ModelsCount: 0,
                MalformedConfig: true));
            return result;
        }

        if (compatModels != null)
        {
            // model -> preset; group by preset.
            var byPreset = compatModels
                .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var grp in byPreset)
            {
                var preset = OpenAiCompatAdapter.Resolve(grp.Key);
                if (preset is null)
                {
                    // Unknown preset name in env -- list it as unhealthy.
                    result.Add(new ProviderProbe(
                        Name: $"compat:{grp.Key}",
                        Endpoint: "(unknown preset)",
                        CredEnvVar: string.Empty,
                        ModelsCount: grp.Count(),
                        UnknownPreset: true));
                    continue;
                }

                result.Add(new ProviderProbe(
                    Name: $"compat:{preset.Name}",
                    Endpoint: preset.BaseUrl.ToString(),
                    CredEnvVar: preset.ApiKeyEnvVar,
                    ModelsCount: grp.Count()));
            }
        }

        return result;
    }

    private static int CountModels(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return 0;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    // -- Probing---------------------------------------------------------

    private static async Task<List<ProviderDoctorEntry>> ProbeAllAsync(List<ProviderProbe> probes)
    {
        if (probes.Count == 0) return new List<ProviderDoctorEntry>();

        var tasks = probes.Select(ProbeAsync).ToArray();
        var entries = await Task.WhenAll(tasks).ConfigureAwait(false);
        return entries.ToList();
    }

    private static async Task<ProviderDoctorEntry> ProbeAsync(ProviderProbe p)
    {
        if (p.MalformedConfig || p.UnknownPreset)
        {
            return new ProviderDoctorEntry(
                Name: p.Name,
                Endpoint: p.Endpoint,
                Dns: "error",
                CredsPresent: false,
                ModelsConfigured: p.ModelsCount,
                Healthy: false);
        }

        // Endpoint -> host. URL parse failure is a hard "error" (not "unreachable").
        string dns;
        if (!Uri.TryCreate(p.Endpoint, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            dns = "error";
        }
        else
        {
            try
            {
                using var cts = new CancellationTokenSource(DnsTimeout);
                var ok = await DnsResolver(uri.Host, cts.Token).ConfigureAwait(false);
                dns = ok ? "ok" : "unreachable";
            }
            catch (OperationCanceledException)
            {
                dns = "unreachable";
            }
            catch
            {
                dns = "error";
            }
        }

        var credsPresent = !string.IsNullOrEmpty(p.CredEnvVar)
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(p.CredEnvVar));

        var healthy = string.Equals(dns, "ok", StringComparison.Ordinal)
            && credsPresent
            && p.ModelsCount > 0;

        return new ProviderDoctorEntry(
            Name: p.Name,
            Endpoint: p.Endpoint,
            Dns: dns,
            CredsPresent: credsPresent,
            ModelsConfigured: p.ModelsCount,
            Healthy: healthy);
    }

    // -- Output formatters (every line through SecretRedactor)-----------

    private static void WriteTable(List<ProviderDoctorEntry> entries, TextWriter w)
    {
        // Column widths.
        const int wName = 18;
        const int wEndpoint = 44;
        const int wDns = 11;
        const int wCreds = 5;
        const int wModels = 6;

        string Row(string a, string b, string c, string d, string e) =>
            "| " + Pad(a, wName) + " | " + Pad(b, wEndpoint) + " | "
            + Pad(c, wDns) + " | " + Pad(d, wCreds) + " | " + Pad(e, wModels) + " |";

        var sep = "+-" + new string('-', wName)
            + "-+-" + new string('-', wEndpoint)
            + "-+-" + new string('-', wDns)
            + "-+-" + new string('-', wCreds)
            + "-+-" + new string('-', wModels) + "-+";

        w.WriteLine(SecretRedactor.Redact(sep));
        w.WriteLine(SecretRedactor.Redact(Row("provider", "endpoint", "dns", "creds", "models")));
        w.WriteLine(SecretRedactor.Redact(sep));
        foreach (var e in entries)
        {
            w.WriteLine(SecretRedactor.Redact(Row(
                e.Name,
                Truncate(e.Endpoint, wEndpoint),
                e.Dns,
                e.CredsPresent ? "yes" : "no",
                e.ModelsConfigured.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
        w.WriteLine(SecretRedactor.Redact(sep));

        var unhealthy = entries.Count(x => !x.Healthy);
        var summary = unhealthy == 0
            ? $"all {entries.Count} provider(s) healthy"
            : $"{unhealthy} of {entries.Count} provider(s) unhealthy";
        w.WriteLine(SecretRedactor.Redact(summary));
    }

    private static void WritePlain(List<ProviderDoctorEntry> entries, TextWriter w)
    {
        // ASCII key:value, one stanza per provider, blank line separator.
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            w.WriteLine(SecretRedactor.Redact($"provider: {e.Name}"));
            w.WriteLine(SecretRedactor.Redact($"endpoint: {e.Endpoint}"));
            w.WriteLine(SecretRedactor.Redact($"dns: {e.Dns}"));
            w.WriteLine(SecretRedactor.Redact($"creds_present: {(e.CredsPresent ? "true" : "false")}"));
            w.WriteLine(SecretRedactor.Redact(
                $"models_configured: {e.ModelsConfigured.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            w.WriteLine(SecretRedactor.Redact($"healthy: {(e.Healthy ? "true" : "false")}"));
            if (i < entries.Count - 1) w.WriteLine();
        }
    }

    private static string Pad(string s, int width)
    {
        if (s.Length >= width) return s;
        return s + new string(' ', width - s.Length);
    }

    private static string Truncate(string s, int width)
    {
        if (s.Length <= width) return s;
        if (width <= 3) return s.Substring(0, width);
        return s.Substring(0, width - 3) + "...";
    }

    /// <summary>Internal probe spec built from the environment.</summary>
    internal sealed record ProviderProbe(
        string Name,
        string Endpoint,
        string CredEnvVar,
        int ModelsCount,
        bool MalformedConfig = false,
        bool UnknownPreset = false);
}
