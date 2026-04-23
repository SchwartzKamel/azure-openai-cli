using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// Windows DPAPI-backed credential store. Uses <c>CryptProtectData</c> /
/// <c>CryptUnprotectData</c> via source-generated P/Invoke to <c>crypt32.dll</c>,
/// scoped to the current user. The encrypted blob is base64-encoded and persisted
/// into <c>UserConfig.ApiKeyCiphertext</c> (written to <c>~/.azureopenai-cli.json</c>).
/// </summary>
/// <remarks>
/// <para>Zero third-party dependencies — pure LOLBin per project ethos. <c>crypt32.dll</c>
/// ships with every Windows release since 2000.</para>
/// <para>AOT-safe: <see cref="LibraryImportAttribute"/> source-generates the marshalling
/// stubs at compile time; no runtime reflection is required.</para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed partial class DpapiCredentialStore : ICredentialStore
{
    private const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    private readonly UserConfig _config;

    public DpapiCredentialStore(UserConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string ProviderName => "dpapi";

    public void Store(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("API key must not be null or empty.", nameof(apiKey));
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(apiKey);
        DATA_BLOB input = default;
        DATA_BLOB output = default;

        try
        {
            input.cbData = (uint)plaintext.Length;
            input.pbData = Marshal.AllocHGlobal(plaintext.Length);
            Marshal.Copy(plaintext, 0, input.pbData, plaintext.Length);

            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN, ref output))
            {
                int err = Marshal.GetLastWin32Error();
                throw new CredentialStoreException(
                    $"DPAPI CryptProtectData failed (Win32 error {err}).");
            }

            byte[] ciphertext = new byte[output.cbData];
            Marshal.Copy(output.pbData, ciphertext, 0, (int)output.cbData);

            _config.ApiKeyCiphertext = Convert.ToBase64String(ciphertext);
            _config.ApiKeyProvider = ProviderName;
            _config.Save();
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }
            if (output.pbData != IntPtr.Zero)
            {
                _ = LocalFree(output.pbData);
            }
            Array.Clear(plaintext, 0, plaintext.Length);
        }
    }

    public string? Retrieve()
    {
        string? blob = _config.ApiKeyCiphertext;
        if (string.IsNullOrEmpty(blob))
        {
            return null;
        }

        byte[] ciphertext;
        try
        {
            ciphertext = Convert.FromBase64String(blob);
        }
        catch (FormatException ex)
        {
            throw new CredentialStoreException(
                "Failed to decrypt stored credential (DPAPI). Was the config moved from another user profile?",
                ex);
        }

        DATA_BLOB input = default;
        DATA_BLOB output = default;
        byte[]? plaintext = null;

        try
        {
            input.cbData = (uint)ciphertext.Length;
            input.pbData = Marshal.AllocHGlobal(ciphertext.Length);
            Marshal.Copy(ciphertext, 0, input.pbData, ciphertext.Length);

            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN, ref output))
            {
                int err = Marshal.GetLastWin32Error();
                throw new CredentialStoreException(
                    $"Failed to decrypt stored credential (DPAPI). Was the config moved from another user profile? (Win32 error {err})",
                    new System.ComponentModel.Win32Exception(err));
            }

            plaintext = new byte[output.cbData];
            Marshal.Copy(output.pbData, plaintext, 0, (int)output.cbData);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CredentialStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CredentialStoreException(
                "Failed to decrypt stored credential (DPAPI). Was the config moved from another user profile?",
                ex);
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }
            if (output.pbData != IntPtr.Zero)
            {
                _ = LocalFree(output.pbData);
            }
            Array.Clear(ciphertext, 0, ciphertext.Length);
            if (plaintext is not null)
            {
                Array.Clear(plaintext, 0, plaintext.Length);
            }
        }
    }

    public void Delete()
    {
        _config.ApiKeyCiphertext = null;
        _config.ApiKeyProvider = null;
        _config.Save();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [LibraryImport("crypt32.dll", SetLastError = true, EntryPoint = "CryptProtectData",
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        ref DATA_BLOB pDataOut);

    [LibraryImport("crypt32.dll", SetLastError = true, EntryPoint = "CryptUnprotectData",
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        ref DATA_BLOB pDataOut);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr LocalFree(IntPtr hMem);
}
