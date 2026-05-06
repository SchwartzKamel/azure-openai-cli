using System;
using System.IO;
using System.Text;

namespace AzureOpenAI_CLI.Cli;

// S03E25 -- The Rotation (Newman). Shared masked-input helper extracted
// for reuse by both SetupWizard (S03E11) and CredsRotate (this episode).
//
// Production path: when the caller hands us Console.In we use
// Console.ReadKey(intercept:true) so the API key never lands in the
// terminal scrollback. Newman audit H-1 invariant: on InvalidOperationException
// (no real TTY behind Console -- pseudo-tty failure or test runner) we
// fail closed and return null. We do NOT silently fall back to
// Console.ReadLine, which would echo plaintext.
//
// Test path: when the caller passes any other TextReader (StringReader
// in unit tests, FileStream in integration tests via heredoc) we read a
// line directly. Tests never see a real terminal, and integration test
// stdin is already isolated to a heredoc.

/// <summary>
/// Read one masked line from <paramref name="stdin"/>. Returns null on
/// Esc / EOF / pseudo-TTY failure. Never echoes the typed characters in
/// plaintext to the terminal scrollback (Newman H-1).
/// </summary>
internal static class MaskedInput
{
    /// <summary>
    /// Read a line of input, masking each character with <c>*</c> when
    /// <paramref name="stdin"/> is the real <see cref="Console.In"/>.
    /// Other readers (test injection) fall through to
    /// <see cref="TextReader.ReadLine"/>.
    /// </summary>
    public static string? ReadMaskedLine(TextReader stdin, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!ReferenceEquals(stdin, Console.In))
        {
            // Hermetic test / piped-stdin path. The caller has already
            // accepted that no terminal-level masking is possible
            // (integration heredoc, unit-test StringReader); we just
            // read a line. The CredsRotate gate refuses to run when
            // stdin is Console.In AND Console.IsInputRedirected, so
            // production never lands here without the operator opting
            // in via a deliberate TextReader injection.
            return stdin.ReadLine();
        }

        try
        {
            var buffer = new StringBuilder();
            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                if (keyInfo.Key == ConsoleKey.Enter) return buffer.ToString();
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length -= 1;
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.Escape) return null;
                if (keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar)) continue;
                buffer.Append(keyInfo.KeyChar);
                Console.Write('*');
            }
        }
        catch (InvalidOperationException)
        {
            // Newman H-1: fail closed; never fall back to ReadLine.
            stderr.WriteLine(
                "[ERROR] Cannot read masked input on this terminal; refusing to "
                + "accept API key in plaintext. Set the appropriate API_KEY "
                + "environment variable instead (see README).");
            return null;
        }
    }
}
