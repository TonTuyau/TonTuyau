namespace TonTuyau.Api.Contracts;

/// <param name="Remember">"Stay logged in": the session lasts 30 days instead of 8 hours.</param>
public sealed record LoginRequest(string? Username, string? Password, bool Remember = false);
