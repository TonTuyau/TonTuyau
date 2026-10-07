using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TonTuyau.FixtureRecorder;

/// <summary>
/// Rewrites recorded TubeArchivist answers so they can be committed: ids, names, titles, descriptions and tags
/// are replaced, consistently across all fixtures (the same channel id always becomes the same fake id).
/// Structure, numbers, dates, codecs, nulls and empty values are kept, so the edge cases stay.
/// </summary>
internal sealed partial class Anonymiser(string salt)
{
    public const int MaxDescriptionLength = 300;
    public const int MaxTags = 8;

    private static readonly string[] Words =
    [
        "alpha", "bravo", "café", "crème", "déjà", "écho", "forêt", "garçon", "hôtel", "île", "jeu", "kilo",
        "lune", "mère", "noël", "œuvre", "piñata", "quai", "rêve", "soleil", "tête", "über", "vidéo", "wagon",
        "xénon", "yaourt", "zèbre", "stream", "short", "live", "tuto", "épisode", "partie", "review", "test",
    ];

    private static readonly HashSet<string> IdKeys = ["youtube_id", "channel_id"];
    private static readonly HashSet<string> TextKeys = ["title", "channel_name"];
    private static readonly HashSet<string> LongTextKeys = ["description", "channel_description"];
    private static readonly HashSet<string> TagKeys = ["tags", "channel_tags"];
    private static readonly HashSet<string> IdListKeys = ["playlist"];

    private readonly Dictionary<string, string> _ids = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> IdMap => _ids;

    public IReadOnlyList<Fixture> Anonymise(IReadOnlyList<Fixture> fixtures)
    {
        // Pass 1: learn every id, so pass 2 can also rewrite ids embedded in paths and query strings.
        foreach (Fixture f in fixtures)
        {
            CollectIds(f.Json);
        }

        return fixtures
            .Select(Anonymise)
            .ToArray();
    }

    private Fixture Anonymise(Fixture f)
    {
        return f with
        {
            Request = ReplaceIds(f.Request),
            Json = f.Json is null ? null : Rewrite(f.Json.DeepClone(), key: null),
            Text = f.Text is null ? null : ReplaceIds(f.Text),
        };
    }

    internal string FakeId(string real)
    {
        if (!_ids.TryGetValue(real, out string? fake))
        {
            string hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(salt + real)))
                .Replace('+', '-')
                .Replace('/', '_');
            fake = real.StartsWith("UC", StringComparison.Ordinal) && real.Length == 24
                ? "UC" + hash[..22]
                : hash[..real.Length];
            _ids[real] = fake;
        }

        return fake;
    }

    internal string FakeText(string real, int maxLength)
    {
        if (real.Length == 0)
        {
            return real;
        }

        int target = Math.Min(real.Length, maxLength);
        Random random = new(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(salt + real)), 0));
        StringBuilder text = new();
        while (text.Length < target)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(Words[random.Next(Words.Length)]);
        }

        return text.ToString()[..target].TrimEnd();
    }

    private void CollectIds(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string? key, JsonNode? value) in obj)
                {
                    if (IdKeys.Contains(key) && value?.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    {
                        FakeId(value.GetValue<string>());
                    }
                    else if (IdListKeys.Contains(key) && value is JsonArray ids)
                    {
                        foreach (JsonValue id in ids.OfType<JsonValue>())
                        {
                            FakeId(id.GetValue<string>());
                        }
                    }
                    else
                    {
                        CollectIds(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    CollectIds(item);
                }

                break;
        }
    }

    private JsonNode? Rewrite(JsonNode? node, string? key)
    {
        switch (node)
        {
            case JsonObject obj:
                string[] names = obj
                    .Select(kv => kv.Key)
                    .ToArray();
                foreach (string? name in names)
                {
                    JsonNode? child = obj[name];
                    JsonNode? rewritten = Rewrite(child, name);
                    if (!ReferenceEquals(child, rewritten))
                    {
                        obj[name] = rewritten;
                    }
                }

                return obj;
            case JsonArray array when key is not null && TagKeys.Contains(key):
                return new JsonArray(array
                    .Take(MaxTags)
                    .Select(t => t is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String
                        ? (JsonNode)JsonValue.Create(FakeText(v.GetValue<string>(), 40))
                        : t?.DeepClone())
                    .ToArray());
            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                {
                    JsonNode? child = array[i];
                    JsonNode? rewritten = Rewrite(child, key);
                    if (!ReferenceEquals(child, rewritten))
                    {
                        array[i] = rewritten;
                    }
                }

                return array;
            case JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.String:
                string s = value.GetValue<string>();
                if (key is not null && TextKeys.Contains(key))
                {
                    return JsonValue.Create(FakeText(s, 100));
                }

                if (key is not null && LongTextKeys.Contains(key))
                {
                    return JsonValue.Create(FakeText(s, MaxDescriptionLength));
                }

                return JsonValue.Create(ReplaceIds(s));
            default:
                return node;
        }
    }

    private string ReplaceIds(string value)
    {
        foreach ((string? real, string? fake) in _ids)
        {
            value = value.Replace(real, fake, StringComparison.Ordinal);
        }

        // TA files thumbnails under the lower-cased first character of the id: keep that true for the fake id.
        return ThumbFolder().Replace(
            value,
            m => $"/cache/videos/{char.ToLowerInvariant(m.Groups["id"].Value[0])}/{m.Groups["id"].Value}.");
    }

    [System.Text.RegularExpressions.GeneratedRegex("/cache/videos/[A-Za-z0-9_-]/(?<id>[A-Za-z0-9_-]{11})\\.")]
    private static partial System.Text.RegularExpressions.Regex ThumbFolder();
}
