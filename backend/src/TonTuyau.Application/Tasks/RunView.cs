using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <param name="Cancellable">It's going on in this backend, so it can be canceled from here.</param>
public sealed record RunView(SyncRun Run, bool Cancellable);
