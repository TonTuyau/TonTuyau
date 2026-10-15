namespace TonTuyau.Application.YouTube;

/// <summary>
/// Which yt-dlp runs (1.5): the copy in the image until another version is active. Read at the start of each yt-dlp
/// run; switched by the updater between tasks only (<see cref="YtdlpService"/>).
/// </summary>
public sealed class YtdlpLocator
{
    private readonly Lock _gate = new();
    private string _path;
    private string? _version;
    private string? _waiting;

    /// <param name="bundledPath">The image's copy (<c>TONTUYAU_YTDLP_PATH</c>).</param>
    public YtdlpLocator(string bundledPath)
    {
        BundledPath = bundledPath;
        _path = bundledPath;
    }

    public string BundledPath { get; }

    /// <summary>The image's copy's version, once read (<c>--version</c>); null before, or when it can't be run.</summary>
    public string? BundledVersion { get; set; }

    public string Path
    {
        get
        {
            lock (_gate)
            {
                return _path;
            }
        }
    }

    /// <summary>The version running; null when it's the image's copy and its version isn't known.</summary>
    public string? Version
    {
        get
        {
            lock (_gate)
            {
                return _version ?? (_path == BundledPath ? BundledVersion : null);
            }
        }
    }

    public bool Bundled
    {
        get
        {
            lock (_gate)
            {
                return _path == BundledPath;
            }
        }
    }

    /// <summary>A version ready, waiting for the task going on to end.</summary>
    public string? Waiting
    {
        get
        {
            lock (_gate)
            {
                return _waiting;
            }
        }

        set
        {
            lock (_gate)
            {
                _waiting = value;
            }
        }
    }

    public void Switch(string path, string version)
    {
        lock (_gate)
        {
            _path = path;
            _version = path == BundledPath ? null : version;
            _waiting = null;
        }
    }
}
