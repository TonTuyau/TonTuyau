namespace TonTuyau.Application.Dashboard;

/// <summary>A column of the dashboard's channel list: its name, or one of its counts.</summary>
public enum DashboardChannelSort
{
    Name,
    Videos,
    Shorts,
    Streams,
    Gone,
    Private,
    Unlisted,
    RetryLater,
    NotChecked,
    Total,

    /// <summary>Its files' sizes together (2.2.0).</summary>
    Size,
}

public enum SortDirection
{
    Ascending,
    Descending,
}
