using System.Text;

namespace AzureOpenAI_CLI.Registry;

// S04E02 Wave 1 -- Embedded Cards (Kramer).
//
// ModelCard is the typed projection of a model-card markdown file's
// YAML-ish front matter. Body prose is intentionally NOT captured -- the
// card is a structured handshake, not a CMS. Anything in the body remains
// addressable by callers that want to fopen() the file directly.
//
// Front-matter contract (between two `---` fences on their own lines):
//
//   ---
//   name: gpt-4o-mini        # required ('model' accepted as alias)
//   provider: azure          # required
//   description: "short..."  # optional
//   status: active           # optional; "active"|"preview"|"deprecated"; default "active"
//   notes: [a, b, c]         # optional; JSON-ish bracketed list
//   ---
//
// Closes FDR S04E01 Wave 2 findings F-01/F-03/F-04 (see ReadCard).
//
// AOT: parsed manually with span-friendly string ops. No YAML lib, no
// reflection, no JsonSerializer path -- so this record does NOT need a
// JsonSerializable registration in AppJsonContext.

/// <summary>
/// Typed front matter for a model card under <c>docs/model-cards/</c>.
/// Produced by <see cref="ModelRegistry.ReadCard"/>; body markdown is
/// not captured here.
/// </summary>
internal sealed record ModelCard(
    string Name,
    string Provider,
    string Description,
    string Status,
    string[] Notes);
