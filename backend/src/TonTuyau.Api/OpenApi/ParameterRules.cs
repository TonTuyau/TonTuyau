using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using TonTuyau.Application.Browse;

namespace TonTuyau.Api.OpenApi;

/// <summary>
/// The query parameters' allowed values for the OpenAPI document, from the same tables the validators use
/// (<see cref="ApiValues"/>, <see cref="RequestRules"/>), so the document can't drift from what the API accepts.
/// </summary>
internal static class ParameterRules
{
    private const string VideoIdPattern = "^[A-Za-z0-9_-]{11}$";
    private const string ChannelIdPattern = "^UC[A-Za-z0-9_-]{22}$";

    /// <summary>Sets the rules of one parameter of one endpoint (<paramref name="path"/> as ApiExplorer gives it).</summary>
    public static void Apply(string path, string name, OpenApiSchema schema)
    {
        bool videoList = path is "api/videos" or "api/channels/{id}/videos";
        switch (name)
        {
            case "size":
                schema.Minimum = "1";
                schema.Maximum = (path == "api/search/suggest" ? ApiValues.MaxSuggestSize : ApiValues.MaxPageSize)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case "type" when videoList || path == "api/search":
                Enum(schema, ApiValues.Types.Names);
                break;
            case "sort" when videoList:
                Enum(schema, ApiValues.VideoSorts.Names);
                break;
            case "sort" when path == "api/channels":
                Enum(schema, ApiValues.ChannelSorts.Names);
                break;
            case "sort" when path == "api/search":
                Enum(schema, ApiValues.SearchSorts.Names);
                break;
            case "kind" when path == "api/search":
                Enum(schema, ApiValues.Kinds.Names);
                break;
            case "published" when path == "api/search":
                Enum(schema, ApiValues.Periods.Names);
                break;
            case "duration" when path == "api/search":
                Enum(schema, ApiValues.Durations.Names);
                break;
            case "period" when path == "api/admin/dashboard/imports":
                Enum(schema, ApiValues.ImportPeriods.Names);
                break;
            case "exclude" when videoList:
                schema.Pattern = VideoIdPattern;
                break;
            case "channel" when path == "api/search":
                schema.Pattern = ChannelIdPattern;
                break;
            case "q":
                Text(schema, path);
                break;
            default:
                break;
        }
    }

    private static void Text(OpenApiSchema schema, string path)
    {
        (int min, int max) = path switch
        {
            "api/search" => (1, ApiValues.MaxSearchLength),
            "api/search/suggest" => (1, ApiValues.MaxSuggestLength),
            _ => (0, ApiValues.MaxChannelFilterLength),
        };
        schema.MinLength = min;
        schema.MaxLength = max;
    }

    private static void Enum(OpenApiSchema schema, IReadOnlyList<string> names) =>
        schema.Enum = [.. names.Select(n => (JsonNode)JsonValue.Create(n))];
}
