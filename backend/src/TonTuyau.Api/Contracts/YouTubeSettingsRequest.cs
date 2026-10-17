namespace TonTuyau.Api.Contracts;

/// <param name="Comments">Which comments a refresh reads: <c>off</c>, <c>top</c> or <c>all</c>; required.</param>
/// <param name="CommentsMax">For <c>top</c>: how many, answers included (1 or more); absent: the one kept.</param>
/// <param name="PauseSeconds">Between two videos of a refresh, 0 or more; absent: the one kept.</param>
/// <param name="HoldMinutes">How long a refresh YouTube refused waits, 1 or more; absent: the one kept.</param>
public sealed record YouTubeSettingsRequest(string? Comments, int? CommentsMax, int? PauseSeconds, int? HoldMinutes);
