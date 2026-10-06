using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KodiakCleaner.Core.Supporter;

/// <summary>What a supporter code says once its signature checks out.</summary>
public sealed record SupporterLicense(string Id, string Name, DateOnly Issued);

public enum CodeStatus
{
    Valid,
    Empty,
    Malformed,
    BadSignature,
    Withdrawn,
    /// <summary>Windows' cryptography couldn't check the code at all (seen under Wine, never on a normal PC).</summary>
    Unverifiable,
}

/// <summary>
/// Supporter codes are "&lt;payload&gt;.&lt;signature&gt;" in base64url. The payload is JSON
/// <c>{"id":"…","name":"…","issued":"2026-10-04"}</c>, signed with Ted's private ECDSA P-256 key (SHA-256).
/// The app checks codes offline against the bundled public key; nothing is sent anywhere.
/// </summary>
public static class SupporterCode
{
    public static (CodeStatus Status, SupporterLicense? License) Verify(string? code, string publicKeyPem, IEnumerable<string>? withdrawnIds = null)
    {
        code = string.Concat((code ?? "").Where(c => !char.IsWhiteSpace(c)));
        if (code.Length == 0) return (CodeStatus.Empty, null);
        var parts = code.Split('.');
        if (parts.Length != 2) return (CodeStatus.Malformed, null);

        byte[] payload, signature;
        try
        {
            payload = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return (CodeStatus.Malformed, null);
        }

        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256)) return (CodeStatus.BadSignature, null);
        }
        catch (CryptographicException)
        {
            return (CodeStatus.Unverifiable, null);
        }

        SupporterLicense? license;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var id = root.GetProperty("id").GetString();
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var issued = root.TryGetProperty("issued", out var i) && DateOnly.TryParse(i.GetString(), out var d) ? d : default;
            license = string.IsNullOrWhiteSpace(id) ? null : new SupporterLicense(id, name, issued);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            license = null;
        }
        if (license is null) return (CodeStatus.Malformed, null);
        if (withdrawnIds?.Contains(license.Id, StringComparer.OrdinalIgnoreCase) == true) return (CodeStatus.Withdrawn, null);
        return (CodeStatus.Valid, license);
    }

    /// <summary>Creates a code. Only the code tool uses this; the app never holds a private key.</summary>
    public static string Create(SupporterLicense license, string privateKeyPem)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
        {
            ["id"] = license.Id,
            ["name"] = license.Name,
            ["issued"] = license.Issued.ToString("yyyy-MM-dd"),
        });
        using var key = ECDsa.Create();
        key.ImportFromPem(privateKeyPem);
        return ToBase64Url(payload) + "." + ToBase64Url(key.SignData(payload, HashAlgorithmName.SHA256));
    }

    /// <summary>One withdrawn code id per line; blank lines and # comments are ignored.</summary>
    public static IReadOnlyList<string> ParseWithdrawnList(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();

    public static string Describe(CodeStatus status) => status switch
    {
        CodeStatus.Valid => "Thank you! Your supporter code is active.",
        CodeStatus.Empty => "Enter the code you received after supporting Kodiak Cleaner.",
        CodeStatus.Malformed => "That doesn't look like a supporter code. Check that you copied all of it.",
        CodeStatus.BadSignature => "That code isn't valid. Check that you copied all of it, or contact us for help.",
        CodeStatus.Withdrawn => "That code has been withdrawn. Contact us if you think this is a mistake.",
        CodeStatus.Unverifiable => "Supporter codes can't be checked on this computer. Contact us for help.",
        _ => "",
    };

    private static string ToBase64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
