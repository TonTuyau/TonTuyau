namespace TonTuyau.Api.Contracts;

/// <summary>A local account an admin makes (contract v2.13).</summary>
/// <param name="Username">1 to 128 characters, no control characters; unique whatever the case.</param>
/// <param name="Password">8 to 1024 characters.</param>
/// <param name="Role"><c>admin</c> or <c>user</c>.</param>
public sealed record NewAccountRequest(string? Username, string? Password, string? Role);
