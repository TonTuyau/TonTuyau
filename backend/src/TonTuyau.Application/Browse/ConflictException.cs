namespace TonTuyau.Application.Browse;

/// <summary>A fine request that the state doesn't allow now (a run going on, nothing to apply…): a 409.</summary>
public sealed class ConflictException(string message, Exception? inner = null) : Exception(message, inner);
