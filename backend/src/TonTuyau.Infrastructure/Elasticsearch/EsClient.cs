using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Store;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>Builds the typed client and turns its responses into <see cref="SearchStoreException"/>s.</summary>
internal static class EsClient
{
    /// <summary>
    /// snake_case everywhere: document properties, enum values and the field names inferred from expressions
    /// (<c>Infer.Field&lt;VideoDocument&gt;(d =&gt; d.ChannelId)</c> → <c>channel_id</c>), matching Definitions/*.json.
    /// </summary>
    public static ElasticsearchClient Create(ElasticsearchOptions options) =>
        Create(options, new HttpRequestInvoker());

    /// <param name="options">Where Elasticsearch is, and who TonTuyau is for it.</param>
    /// <param name="requestInvoker">What sends the requests: HTTP, or canned answers in unit tests.</param>
    public static ElasticsearchClient Create(ElasticsearchOptions options, IRequestInvoker requestInvoker)
    {
        ArgumentNullException.ThrowIfNull(options);
        ElasticsearchClientSettings settings = new ElasticsearchClientSettings(
            new SingleNodePool(new Uri(options.Url)),
            requestInvoker,
            (_, s) => new DefaultSourceSerializer(s, ConfigureJson))
            .DefaultFieldNameInferrer(JsonNamingPolicy.SnakeCaseLower.ConvertName)
            .Authentication(new BasicAuthentication(options.Username, options.Password))
            .RequestTimeout(TimeSpan.FromSeconds(30));
        return new ElasticsearchClient(settings);
    }

    /// <summary>Throws unless the call succeeded or answered one of the accepted statuses (e.g. 404, 409).</summary>
    public static T Ensure<T>(this T response, string what, params int[] accepted)
        where T : ElasticsearchResponse
    {
        ArgumentNullException.ThrowIfNull(response);
        int? status = response.ApiCallDetails.HttpStatusCode;
        if (response.IsValidResponse || (status is { } s && accepted.Contains(s)))
        {
            return response;
        }

        if (status is null)
        {
            throw new SearchStoreException(
                $"{what}: Elasticsearch can't be reached.",
                response.ApiCallDetails.OriginalException);
        }

        string reason = response.ElasticsearchServerError?.Error is { } error
            ? $"{error.Type} {error.Reason}"
            : "(no error details)";
        throw new SearchStoreException($"{what}: Elasticsearch answered {status}: {reason}");
    }

    public static int? Status(this ElasticsearchResponse response) => response.ApiCallDetails.HttpStatusCode;

    /// <summary>Creates an index from a definition file (settings + mappings): data, not a query, so sent as is.</summary>
    /// <returns>False when it already existed.</returns>
    public static async Task<bool> CreateIndexFromDefinitionAsync(
        this ElasticsearchClient client,
        string index,
        string definition,
        CancellationToken cancellationToken)
    {
        StringResponse response = await client.Transport
            .RequestAsync<StringResponse>(
                Elastic.Transport.HttpMethod.PUT,
                $"/{index}",
                PostData.String(definition),
                cancellationToken)
            .ConfigureAwait(false);
        if (response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            return true;
        }

        if (response.ApiCallDetails.HttpStatusCode == 400
            && response.Body.Contains("resource_already_exists_exception", StringComparison.Ordinal))
        {
            return false;
        }

        throw new SearchStoreException(
            $"Creating {index}: Elasticsearch answered {response.ApiCallDetails.HttpStatusCode?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}: "
                + response.Body[..Math.Min(response.Body.Length, 300)],
            response.ApiCallDetails.OriginalException);
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        // First in the list: the client's own enum converter (PascalCase names) must not win.
        options.Converters.Insert(0, new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }
}
