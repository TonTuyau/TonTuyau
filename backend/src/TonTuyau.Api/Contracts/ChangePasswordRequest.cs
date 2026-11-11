namespace TonTuyau.Api.Contracts;

/// <summary>One's own new password (contract v2.13, My account).</summary>
/// <param name="Current">The password now: checked first.</param>
/// <param name="Password">The new one, 8 to 1024 characters.</param>
public sealed record ChangePasswordRequest(string? Current, string? Password);
