namespace TonTuyau.Api.Contracts;

/// <param name="ReadComments">Whether a refresh reads each video's comments too; required.</param>
public sealed record YouTubeSettingsRequest(bool? ReadComments);
