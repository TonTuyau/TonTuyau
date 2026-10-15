namespace TonTuyau.Application.YouTube;

/// <summary>Wakes the yt-dlp updater when the choice changes, instead of waiting for its next daily check.</summary>
public sealed class YtdlpUpdateSignal : IDisposable
{
    private readonly SemaphoreSlim _wake = new(0, 1);

    public void Wake()
    {
        if (_wake.CurrentCount == 0)
        {
            try
            {
                _wake.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already woken: once is enough.
            }
        }
    }

    /// <summary>Waits until woken or <paramref name="timeout"/> passed.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _wake.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _wake.Dispose();
}
