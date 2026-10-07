using System.Reflection;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// TonTuyau's version (<c>Version</c> in Directory.Build.props), shown by <c>tontuyau --version</c> and on every answer
/// in the <c>X-TonTuyau-Version</c> header.
/// </summary>
internal static class AppVersion
{
    public const string HeaderName = "X-TonTuyau-Version";

    public static string Value { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    /// <summary>Middleware: the version header on every answer.</summary>
    public static Task AddHeader(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        context.Response.Headers[HeaderName] = Value;
        return next(context);
    }
}
