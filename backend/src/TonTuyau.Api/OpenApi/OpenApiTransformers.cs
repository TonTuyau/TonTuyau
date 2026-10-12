using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using TonTuyau.Api.Contracts;

namespace TonTuyau.Api.OpenApi;

/// <summary>What the generated OpenAPI document needs on top of what ASP.NET Core infers.</summary>
internal static class OpenApiTransformers
{
    public static OpenApiOptions AddTonTuyauTransformers(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "TonTuyau API",
                Version = "v1",
                Description = "REST API of TonTuyau (docs/API_CONTRACT.md). Lists are cursor-paged; "
                    + "errors are RFC 9457 ProblemDetails.",
            };

            // The URLs are relative to wherever the API is reached: no server address in the document, so it reads
            // the same from any host (the front commits a copy, QA10 M5-4).
            document.Servers = [];
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(OneOfSuccessShapesAsync);
        options.AddOperationTransformer(ParameterValuesAsync);
        options.AddOperationTransformer(JsonOnlyAsync);
        options.AddSchemaTransformer(ContractSchemasAsync);
        options.AddDocumentTransformer(ReferenceSearchAllPartsAsync);
        return options;
    }

    /// <summary>
    /// <c>SearchAllResponse</c> is made by <see cref="OneOfSuccessShapesAsync"/>, where ASP.NET Core 10 writes its
    /// nested shapes inline. Its two members are components already: point to them, so generated types share them.
    /// </summary>
    private static Task ReferenceSearchAllPartsAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        IDictionary<string, IOpenApiSchema>? schemas = document.Components?.Schemas;
        if (schemas is null
            || !schemas.ContainsKey(nameof(SearchAllResponse))
            || !schemas.ContainsKey(nameof(ChannelCard))
            || !schemas.ContainsKey("PageOfVideoCard"))
        {
            return Task.CompletedTask;
        }

        schemas[nameof(SearchAllResponse)] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string>(StringComparer.Ordinal) { "channels", "videos" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["channels"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(nameof(ChannelCard), document),
                },
                ["videos"] = new OpenApiSchemaReference("PageOfVideoCard", document),
            },
        };
        return Task.CompletedTask;
    }

    /// <summary>
    /// The API answers JSON (contract): ASP.NET Core also lists text/plain and text/json for typed actions, which
    /// generated clients would then have to handle.
    /// </summary>
    private static Task JsonOnlyAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (IOpenApiResponse response in operation.Responses.Values)
        {
            if (response.Content is { } content && content.ContainsKey("application/json"))
            {
                content.Remove("text/plain");
                content.Remove("text/json");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The values each query parameter takes, as the validators check them (<see cref="ApiValues"/>): enums, size
    /// ranges, text lengths, id formats. ASP.NET Core only sees strings and integers.
    /// </summary>
    private static Task ParameterValuesAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        string path = context.Description.RelativePath ?? "";
        foreach (IOpenApiParameter parameter in operation.Parameters ?? [])
        {
            if (parameter.In == ParameterLocation.Query
                && parameter.Name is { } name
                && parameter.Schema is OpenApiSchema schema)
            {
                ParameterRules.Apply(path, name, schema);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// What the contract says that ASP.NET Core can't infer: enums are strings; a ProblemDetails always has its
    /// <c>title</c> and <c>status</c> (and a <c>traceId</c>); its optional members are left out rather than null.
    /// </summary>
    private static Task ContractSchemasAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type type = context.JsonTypeInfo.Type;
        if (type.IsEnum)
        {
            schema.Type = JsonSchemaType.String;
        }

        if (type == typeof(ProblemDetails) || type == typeof(HttpValidationProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            foreach (string optional in (string[])["type", "detail", "instance"])
            {
                schema.Properties[optional] = new OpenApiSchema { Type = JsonSchemaType.String };
            }

            schema.Properties["title"] = new OpenApiSchema { Type = JsonSchemaType.String };
            schema.Properties["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
            schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String };
            schema.Required = new HashSet<string>(StringComparer.Ordinal) { "title", "status" };
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// An action declaring several 200 shapes (<c>/api/search</c>, by kind) gets <c>oneOf</c> them, instead of the
    /// single one ASP.NET Core would keep.
    /// </summary>
    private static async Task OneOfSuccessShapesAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // ApiExplorer keeps one type per status: the declared ones are in the endpoint's metadata.
        Type[] shapes = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<ProducesResponseTypeAttribute>()
            .Where(r => r.StatusCode == StatusCodes.Status200OK && r.Type != typeof(void))
            .Select(r => r.Type)
            .Distinct()
            .ToArray();
        if (shapes.Length < 2
            || operation.Responses is null
            || !operation.Responses.TryGetValue("200", out IOpenApiResponse? success)
            || success.Content is null
            || !success.Content.TryGetValue("application/json", out OpenApiMediaType? json))
        {
            return;
        }

        OpenApiDocument document = context.Document!;
        List<IOpenApiSchema> schemas = [];
        foreach (Type shape in shapes)
        {
            // Same ids as ASP.NET Core's own (Page<VideoCard> → PageOfVideoCard), so each shape is one component.
            string id = SchemaId(shape);
            if (document.Components?.Schemas?.ContainsKey(id) != true)
            {
                OpenApiSchema schema = await context
                    .GetOrCreateSchemaAsync(shape, null, cancellationToken)
                    .ConfigureAwait(false);
                document.AddComponent<IOpenApiSchema>(id, schema);
            }

            schemas.Add(new OpenApiSchemaReference(id, document));
        }

        json.Schema = new OpenApiSchema { OneOf = schemas };

        // An IActionResult also lists text/plain and text/json; the API only answers application/json.
        string[] others = success.Content.Keys
            .Where(k => k != "application/json")
            .ToArray();
        foreach (string other in others)
        {
            success.Content.Remove(other);
        }
    }

    private static string SchemaId(Type type) =>
        type.IsGenericType
            ? type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]
                + "Of"
                + string.Concat(type
                    .GetGenericArguments()
                    .Select(SchemaId))
            : type.Name;

}
