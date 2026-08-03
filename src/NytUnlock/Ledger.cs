using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NytUnlock;

internal sealed class LedgerEntry
{
    public string ArticleUri { get; set; } = "";
    public string? Headline { get; set; }
    public string? WebUrl { get; set; }
    public string? OrderId { get; set; }
    public string? InvoiceId { get; set; }
    public string? AmountValue { get; set; }

    /// <summary>CREATED · GRANTED · PARTIALLY_REFUNDED · REVOKED</summary>
    public string Status { get; set; } = "CREATED";

    public string? CaptureId { get; set; }
    public string? RefundId { get; set; }

    /// <summary>
    /// Stored so the early-exit path in `buy` can reprint a token without re-minting,
    /// and so expiry is checkable. BUGS.md BUG-14: the planned schema had neither.
    /// </summary>
    public string? Token { get; set; }
    public long? ExpiresAtUnix { get; set; }

    public string? CreatedAt { get; set; }
    public string? CapturedAt { get; set; }
    public string? RefundedAt { get; set; }
}

internal static class Ledger
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static List<LedgerEntry> Load()
    {
        if (!File.Exists(Config.LedgerPath)) return [];
        var text = File.ReadAllText(Config.LedgerPath);
        if (string.IsNullOrWhiteSpace(text)) return [];
        return JsonSerializer.Deserialize<List<LedgerEntry>>(text, Json) ?? [];
    }

    public static void Save(List<LedgerEntry> entries)
        => File.WriteAllText(Config.LedgerPath, JsonSerializer.Serialize(entries, Json));

    public static LedgerEntry? Find(string articleUri)
        => Load().FirstOrDefault(e => e.ArticleUri == articleUri);

    public static void Upsert(LedgerEntry entry)
    {
        var all = Load();
        var i = all.FindIndex(e => e.ArticleUri == entry.ArticleUri);
        if (i >= 0) all[i] = entry; else all.Add(entry);
        Save(all);
    }

    // ---- access token -------------------------------------------------------

    private static string B64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Mints base64url(articleUri) + "." + expiryUnix + "." + base64url(HMAC of that prefix).
    ///
    /// BUGS.md BUG-21: the planned format concatenated a raw article id with '.' as the
    /// delimiter, but NYT uris contain arbitrary characters, so the expiry boundary was
    /// ambiguous. Base64url output contains no '.', so splitting is unambiguous, and the
    /// HMAC input is stated exactly: the "&lt;b64uri&gt;.&lt;exp&gt;" prefix, verbatim.
    /// </summary>
    public static (string Token, long ExpiresAtUnix) Mint(string articleUri, TimeSpan lifetime)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = $"{B64Url(Encoding.UTF8.GetBytes(articleUri))}.{exp}";
        return ($"{payload}.{Sign(payload)}", exp);
    }

    private static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Config.SigningSecret()));
        return B64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>
    /// Verifies signature, expiry, and that the ledger still grants access. A token whose
    /// entry was revoked by a full refund must stop validating — which is what makes the
    /// refund acceptance criterion testable at all (BUGS.md BUG-15).
    /// </summary>
    public static (bool Ok, string Reason) Verify(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return (false, "malformed token");

        var payload = $"{parts[0]}.{parts[1]}";
        var expected = Sign(payload);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[2])))
        {
            return (false, "bad signature");
        }

        if (!long.TryParse(parts[1], out var exp)) return (false, "malformed expiry");
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp) return (false, "expired");

        string uri;
        try
        {
            var b64 = parts[0].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            uri = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }
        catch
        {
            return (false, "malformed subject");
        }

        var entry = Find(uri);
        if (entry is null) return (false, "no ledger entry");

        return entry.Status switch
        {
            "GRANTED" or "PARTIALLY_REFUNDED" => (true, $"valid until {DateTimeOffset.FromUnixTimeSeconds(exp):u} for {uri}"),
            "REVOKED" => (false, "access revoked by refund"),
            _ => (false, $"not granted (status {entry.Status})"),
        };
    }
}
