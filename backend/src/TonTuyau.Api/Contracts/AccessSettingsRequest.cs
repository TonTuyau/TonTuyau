namespace TonTuyau.Api.Contracts;

/// <summary>Who can watch (contract v2.13).</summary>
/// <param name="AccountsOnly">True: the logged-in accounts only; false: everyone. Required.</param>
public sealed record AccessSettingsRequest(bool? AccountsOnly);
