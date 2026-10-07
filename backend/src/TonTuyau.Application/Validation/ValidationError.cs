namespace TonTuyau.Application.Validation;

/// <summary>One broken rule: which field, and why.</summary>
public sealed record ValidationError(string Field, string Message);
