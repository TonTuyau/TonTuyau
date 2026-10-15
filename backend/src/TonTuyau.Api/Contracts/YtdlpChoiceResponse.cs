using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <param name="Kind"><c>latest</c> (the latest official release, kept up to date), <c>official</c>, <c>nightly</c>.</param>
/// <param name="Version">The version chosen, for <c>official</c> and <c>nightly</c>; null for <c>latest</c>.</param>
/// <param name="ChosenBy">The account; null while nobody chose (the default).</param>
public sealed record YtdlpChoiceResponse(YtdlpChoiceKind Kind, string? Version, string? ChosenBy, DateTime? ChosenAt);
