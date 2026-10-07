namespace TonTuyau.Application.Browse;

/// <summary>The id is well-formed but nothing has it (or its file or image isn't there): a 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);
