namespace TonTuyau.Application.YouTube;

/// <summary>
/// "Add a channel" can't be done (2.0, B4): YouTube says the channel is gone or private, or gave no answer to trust.
/// The run fails with the reason; YouTube refusing TonTuyau puts it on hold instead.
/// </summary>
public sealed class ChannelNotAddedException : Exception
{
    public ChannelNotAddedException()
    {
    }

    public ChannelNotAddedException(string message)
        : base(message)
    {
    }

    public ChannelNotAddedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
