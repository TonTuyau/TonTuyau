using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <param name="Cancellable">It's going on in this backend, on hold, or waiting in the queue: it can be canceled.</param>
/// <param name="Position">Waiting in the queue (2.0): its place in line, 1 for the next to start; null otherwise.</param>
public sealed record RunView(SyncRun Run, bool Cancellable, int? Position = null);
