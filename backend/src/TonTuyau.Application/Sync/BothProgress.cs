namespace TonTuyau.Application.Sync;

/// <summary>Tells two reporters: the caller's (the CLI's lines) and the run's log (1.4).</summary>
internal sealed class BothProgress(ISyncProgressReporter first, ISyncProgressReporter second) : ISyncProgressReporter
{
    public void Report(SyncProgress progress)
    {
        first.Report(progress);
        second.Report(progress);
    }
}
