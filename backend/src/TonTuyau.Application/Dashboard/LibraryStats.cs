namespace TonTuyau.Application.Dashboard;

/// <param name="Types">Each type that has videos (a type without any is left out).</param>
/// <param name="Gone">Videos no longer available on YouTube (TubeArchivist's <c>active</c> flag is false).</param>
/// <param name="Channels">By channel id; a channel without videos is left out.</param>
public sealed record LibraryStats(
    IReadOnlyList<TypeStats> Types,
    long Gone,
    IReadOnlyDictionary<string, ChannelStats> Channels);
