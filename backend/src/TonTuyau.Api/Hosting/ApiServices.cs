using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.OpenApi;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Media;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.Hosting;

/// <summary>The REST API: services, validators, controllers, JSON and OpenAPI. Web host only (not the CLI).</summary>
internal static class ApiServices
{
    public static IServiceCollection AddTonTuyauApi(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<HomeRequest>, HomeRequestValidator>();
        services.AddSingleton<IValidator<VideoListRequest>, VideoListRequestValidator>();
        services.AddSingleton<IValidator<ChannelListRequest>, ChannelListRequestValidator>();
        services.AddSingleton<IValidator<ChannelVideosRequest>, ChannelVideosRequestValidator>();
        services.AddSingleton<IValidator<VideoSearchRequest>, VideoSearchRequestValidator>();
        services.AddSingleton<IValidator<SuggestRequest>, SuggestRequestValidator>();
        services.AddSingleton<IValidator<ImportHistoryRequest>, ImportHistoryRequestValidator>();
        services.AddSingleton<CardBuilder>();
        services.AddSingleton<VideoService>();
        services.AddSingleton<ChannelService>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<DashboardService>();

        services
            .AddControllers(o =>
            {
                // Query values that don't even bind (size=abc) get the same 400 as the validators' ones.
                o.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, _) => "must be a whole number.");
                o.ModelBindingMessageProvider.SetNonPropertyAttemptedValueIsInvalidAccessor(
                    _ => "must be a whole number.");
            })
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
                throw new ValidationException(RequestRules.Subject, BindingErrors(context.ModelState)))
            .AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));

        // The OpenAPI document is built from these (minimal API) options: keep them the same as the controllers'.
        services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));
        services.AddOpenApi(o => o.AddTonTuyauTransformers());
        return services;
    }

    /// <summary>
    /// camelCase (the default), enums as lower-case strings (<c>"video"</c>, <c>"short"</c>…), numbers as numbers
    /// (the web default also reads them from strings, which the OpenAPI document would then allow), and accents
    /// written as they are rather than as <c>\u00e9</c>.
    /// </summary>
    internal static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    }

    private static ValidationError[] BindingErrors(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary state) =>
        state
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .SelectMany(kv => kv.Value!.Errors.Select(e => new ValidationError(kv.Key, e.ErrorMessage)))
            .ToArray();
}
