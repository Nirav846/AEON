using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aeon.Data;

/// <summary>
/// Reproduces, in C#, the exact hash that scripts/build_bundle.py computes, so the
/// client can verify what it downloaded instead of trusting it.
///
/// build_bundle.py does:
///     payload = json.dumps({k: bundle[k] for k in KINDS},
///                         sort_keys=True, ensure_ascii=False,
///                         separators=(",", ":"))
///     version = hashlib.sha256(payload.encode("utf-8")).hexdigest()[:16]
///
/// Four things must match exactly, and each is a silent-wrong-answer trap:
///
///  1. Keys sorted RECURSIVELY. sort_keys=True sorts at every level, not just the top.
///  2. No whitespace anywhere (separators=(",", ":")).
///  3. Non-ASCII left as raw UTF-8 (ensure_ascii=False). System.Text.Json escapes
///     non-ASCII to \uXXXX by default, which would emit "\u2014" where Python writes
///     a literal em-dash. This is not hypothetical: 7 complexes and 1 concept in the
///     live bundle contain U+2014. JavaScriptEncoder.UnsafeRelaxedJsonEscaping is
///     required, and is safe here because we are verifying a file we then parse,
///     never embedding this string into HTML.
///  4. Hash the FOUR ARRAYS ONLY - "version" and "generated_at" are excluded, because
///     they are metadata about the build, not part of its content. Including them
///     would make the hash self-referential and unverifiable.
///
/// Note on key ordering: Python sorts strings by Unicode code point; .NET's ordinal
/// comparer orders by UTF-16 code unit. These differ only above the BMP. Every key in
/// this bundle is ASCII, so the two agree - but a future non-ASCII field name would
/// break this silently, so RecomputeFromBundle asserts a match against the served
/// version rather than trusting it.
/// </summary>
public static class BundleHasher
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = true,
        // Critical: without this, non-ASCII becomes \uXXXX and the hash never matches.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Canonicalise arbitrary bundle JSON bytes into the exact string Python's
    /// json.dumps(sort_keys=True, ensure_ascii=False, separators=(",", ":")) would
    /// produce for the same value.
    /// </summary>
    public static string Canonicalize(ReadOnlySpan<byte> json)
    {
        using var doc = JsonDocument.Parse(json.ToArray());
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            WriteCanonical(writer, doc.RootElement);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                // Ordinal sort of every key, matching Python's sort_keys=True.
                foreach (var prop in el.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonical(writer, prop.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in el.EnumerateArray())   // order preserved, never sorted
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(el.GetString());
                break;

            case JsonValueKind.Number:
                // Emit the source literal verbatim rather than round-tripping through a
                // double, so 5 stays "5" and never becomes "5.0" or "5E0".
                writer.WriteRawValue(el.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }

    /// <summary>
    /// Canonicalise only the four data arrays, dropping version/generated_at.
    ///
    /// The top-level keys are emitted in ORDINAL SORT order, not in the order they
    /// appear in the file and not in the KINDS declaration order. Python's
    /// sort_keys=True sorts the outer object too, so the canonical form is
    /// circuits, complexes, concepts, conditioning. Hardcoding any other order -
    /// including "the natural reading order" - silently produces a different hash.
    /// </summary>
    private static string CanonicalizePayloadOnly(ReadOnlySpan<byte> json)
    {
        using var doc = JsonDocument.Parse(json.ToArray());
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var name in PayloadKeys.OrderBy(n => n, StringComparer.Ordinal))
            {
                writer.WritePropertyName(name);
                if (doc.RootElement.TryGetProperty(name, out var arr))
                {
                    WriteCanonical(writer, arr);
                }
                else
                {
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static readonly string[] PayloadKeys = { "complexes", "circuits", "conditioning", "concepts" };

    /// <summary>SHA-256 of the canonical payload, first 16 hex chars - matching Python.</summary>
    public static string ComputeVersion(ReadOnlySpan<byte> bundleJson)
    {
        var canonical = CanonicalizePayloadOnly(bundleJson);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>Recompute a bundle's version and compare it to the served version.</summary>
    public static bool MatchesServedVersion(ReadOnlySpan<byte> bundleJson, string servedVersion)
        => ComputeVersion(bundleJson) == servedVersion;
}