namespace TonTuyau.Application.Sync;

/// <summary>A run <see cref="SyncRunner"/> started: its id at once, its end to wait for if wanted.</summary>
public sealed record StartedRun(string Id, Task<SyncRun> Finished);
