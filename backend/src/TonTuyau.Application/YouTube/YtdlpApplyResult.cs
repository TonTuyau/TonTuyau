namespace TonTuyau.Application.YouTube;

/// <summary>What applying the choice did (1.5).</summary>
public enum YtdlpApplyResult
{
    /// <summary>The version wanted is the one running (or was just switched to).</summary>
    UpToDate,

    /// <summary>The version wanted is ready, and waits for the task going on to end.</summary>
    WaitingForTask,

    /// <summary>It couldn't be done (GitHub unreachable, a checksum wrong…): the version running stays.</summary>
    Failed,
}
