using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Paging;

/// <summary>
/// Turns a <see cref="SortPosition"/> into the opaque cursor the API hands out, and back. A cursor carries a
/// fingerprint of the query (list, filters, sort) it was made for: used with another query, it is refused.
/// </summary>
/// <remarks>
/// Format: base64url (no padding) of <c>{"f":"&lt;fingerprint&gt;","a":[{"s":"x"},{"i":1},{"r":1.5}],"t":…}</c>;
/// <c>t</c> (Unix milliseconds) is the frozen <c>published</c> cut-off of a search, when it has one.
/// </remarks>
public static class CursorCodec
{
    public const string Field = "cursor";
    public const int MaxLength = 2048;
    public const string NotACursor = "isn't a cursor given by this API.";
    public const string OtherQuery =
        "belongs to another list (other filters or sort): start again from the first page.";

    /// <summary>Short hash of the canonical description of a query (see the services' <c>Fingerprint</c>).</summary>
    public static string Fingerprint(string canonicalQuery)
    {
        ArgumentNullException.ThrowIfNull(canonicalQuery);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalQuery)))[..16];
    }

    public static string Encode(string fingerprint, SortPosition position, DateTimeOffset? since = null)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter json = new(buffer))
        {
            json.WriteStartObject();
            json.WriteString("f", fingerprint);
            json.WriteStartArray("a");
            foreach (SortValue value in position.Values)
            {
                json.WriteStartObject();
                switch (value)
                {
                    case TextSortValue text:
                        json.WriteString("s", text.Value);
                        break;
                    case IntegerSortValue integer:
                        json.WriteNumber("i", integer.Value);
                        break;
                    case RealSortValue real:
                        json.WriteNumber("r", real.Value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(position), value, "Unknown sort value.");
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
            if (since is not null)
            {
                json.WriteNumber("t", since.Value.ToUnixTimeMilliseconds());
            }

            json.WriteEndObject();
        }

        return Base64Url.EncodeToString(buffer.WrittenSpan);
    }

    /// <returns>Null when there's no cursor (first page).</returns>
    /// <exception cref="ValidationException">Not a cursor of ours, or one made for another query.</exception>
    public static PageCursor? Decode(string? cursor, string fingerprint)
    {
        if (cursor is null)
        {
            return null;
        }

        (string madeFor, PageCursor page) = Read(cursor) ?? throw Invalid(NotACursor);
        return string.Equals(madeFor, fingerprint, StringComparison.Ordinal) ? page : throw Invalid(OtherQuery);
    }

    /// <summary>A cursor that decodes but doesn't fit the query's sort (e.g. hand-edited): same answer as garbage.</summary>
    public static ValidationException Invalid(string message = NotACursor)
    {
        ValidationError error = new(Field, message);
        return new ValidationException("request", [error]);
    }

    private static (string, PageCursor)? Read(string cursor)
    {
        if (cursor.Length is 0 or > MaxLength || !Base64Url.IsValid(cursor))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(Base64Url.DecodeFromChars(cursor));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("f", out JsonElement fingerprint)
                || fingerprint.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("a", out JsonElement after)
                || after.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            List<SortValue> values = [];
            foreach (JsonElement item in after.EnumerateArray())
            {
                SortValue? value = ReadValue(item);
                if (value is null)
                {
                    return null;
                }

                values.Add(value);
            }

            DateTimeOffset? since = null;
            if (root.TryGetProperty("t", out JsonElement t))
            {
                if (t.ValueKind != JsonValueKind.Number
                    || !t.TryGetInt64(out long milliseconds)
                    || milliseconds < DateTimeOffset.MinValue.ToUnixTimeMilliseconds()
                    || milliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
                {
                    return null;
                }

                since = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            }

            return values.Count == 0
                ? null
                : (fingerprint.GetString()!, new PageCursor(new SortPosition(values), since));
        }
        catch (JsonException)
        {
            // Not JSON inside: the cursor wasn't made by us (a 400 for the caller, nothing to log).
            throw Invalid(NotACursor);
        }
    }

    private static SortValue? ReadValue(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonProperty[] all = [.. item.EnumerateObject()];
        if (all.Length != 1)
        {
            return null;
        }

        JsonProperty only = all[0];
        return (only.Name, only.Value.ValueKind) switch
        {
            ("s", JsonValueKind.String) => new TextSortValue(only.Value.GetString()!),
            ("i", JsonValueKind.Number) when only.Value.TryGetInt64(out long integer) => new IntegerSortValue(integer),
            ("r", JsonValueKind.Number) when only.Value.TryGetDouble(out double real) && double.IsFinite(real) =>
                new RealSortValue(real),
            _ => null,
        };
    }
}
