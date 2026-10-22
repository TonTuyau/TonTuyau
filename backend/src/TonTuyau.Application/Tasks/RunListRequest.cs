namespace TonTuyau.Application.Tasks;

/// <param name="Task">
/// import | indexRebuild | refreshVideo | refreshChannel | refreshChannelVideos; null for every task.
/// </param>
/// <param name="Before">The last run of the previous page (its <c>nextCursor</c>): the runs started before it.</param>
public sealed record RunListRequest(string? Task, string? Before, int? Size);
