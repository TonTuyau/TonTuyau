namespace TonTuyau.Api.Contracts;

/// <summary>
/// What changes in an account (contract v2.13); each absent: kept. A directory account: <paramref name="Disabled"/>
/// only.
/// </summary>
/// <param name="Role"><c>admin</c> or <c>user</c>.</param>
/// <param name="Password">A new password, 8 to 1024 characters.</param>
/// <param name="Disabled">Refused at login from now on, its sessions ended within minutes; false: enabled again.</param>
public sealed record AccountChangeRequest(string? Role = null, string? Password = null, bool? Disabled = null);
