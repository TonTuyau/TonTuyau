namespace TonTuyau.Api.Contracts;

/// <param name="Kind"><c>latest</c>, <c>official</c> or <c>nightly</c>.</param>
/// <param name="Version">For <c>official</c> and <c>nightly</c>: one of the versions offered.</param>
public sealed record ChooseYtdlpRequest(string? Kind, string? Version);
