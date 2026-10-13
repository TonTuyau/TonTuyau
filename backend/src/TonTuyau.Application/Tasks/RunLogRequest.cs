namespace TonTuyau.Application.Tasks;

/// <param name="After">The last line read (its id): the lines after it. Null or 0 from the first.</param>
public sealed record RunLogRequest(long? After, int? Size);
