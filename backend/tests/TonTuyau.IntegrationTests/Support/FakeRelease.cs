namespace TonTuyau.IntegrationTests.Support;

/// <summary>A yt-dlp release on the fake GitHub; without a Linux program, like the releases before 2022.06.22.</summary>
public sealed record FakeRelease(string Version, DateTimeOffset PublishedAt, bool HasLinuxProgram = true);
