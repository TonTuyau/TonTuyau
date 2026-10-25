using TonTuyau.Application.Accounts;
using TonTuyau.Application.ApiKeys;
using TonTuyau.Application.Comments;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Browse;

public enum SearchKind
{
    All,
    Videos,
    Channels,
}

public enum ChannelArtKind
{
    Thumb,
    Banner,
}

/// <summary>
/// The values the API accepts in its parameters (API_CONTRACT.md), and what they mean. Validators check against
/// these tables, services read them once the request is valid: one place for both.
/// </summary>
public static class ApiValues
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;
    public const int DefaultSuggestSize = 8;
    public const int MaxSuggestSize = 20;
    public const int MaxSearchLength = 200;
    public const int MaxSuggestLength = 100;
    public const int MaxChannelFilterLength = 100;

    /// <summary>How far back a <c>published</c> period reaches from now.</summary>
    public static TimeSpan LengthOf(PublishedPeriod period) => period switch
    {
        PublishedPeriod.Today => TimeSpan.FromHours(24),
        PublishedPeriod.Week => TimeSpan.FromDays(7),
        PublishedPeriod.Month => TimeSpan.FromDays(30),
        PublishedPeriod.Year => TimeSpan.FromDays(365),
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    /// <summary>How many channels <c>kind=all</c> shows above the videos.</summary>
    public const int TopChannels = 3;

    /// <summary>At most this many channels open the suggestions; video titles fill the rest (M4-Q2).</summary>
    public const int SuggestedChannels = 3;

    public static readonly Table<VideoType> Types = new()
    {
        ["video"] = VideoType.Video,
        ["short"] = VideoType.Short,
        ["stream"] = VideoType.Stream,
    };

    public static readonly Table<VideoListSort> VideoSorts = new()
    {
        ["latest"] = VideoListSort.Latest,
        ["popular"] = VideoListSort.Popular,
        ["oldest"] = VideoListSort.Oldest,
    };

    public static readonly Table<VideoListSort> SearchSorts = new()
    {
        ["relevance"] = VideoListSort.Relevance,
        ["latest"] = VideoListSort.Latest,
        ["popular"] = VideoListSort.Popular,
    };

    public static readonly Table<ChannelListSort> ChannelSorts = new()
    {
        ["name"] = ChannelListSort.Name,
        ["subscribers"] = ChannelListSort.Subscribers,
    };

    public static readonly Table<SearchKind> Kinds = new()
    {
        ["all"] = SearchKind.All,
        ["videos"] = SearchKind.Videos,
        ["channels"] = SearchKind.Channels,
    };

    public static readonly Table<PublishedPeriod> Periods = new()
    {
        ["today"] = PublishedPeriod.Today,
        ["week"] = PublishedPeriod.Week,
        ["month"] = PublishedPeriod.Month,
        ["year"] = PublishedPeriod.Year,
    };

    /// <summary>The import history's periods (the admin dashboard).</summary>
    /// <summary>The kinds of run (1.4), as the Tasks page and the run history name them.</summary>
    public static readonly Table<SyncMode> Tasks = new()
    {
        ["import"] = SyncMode.Import,
        ["indexRebuild"] = SyncMode.IndexRebuild,
        ["refreshVideo"] = SyncMode.RefreshVideo,
        ["refreshChannel"] = SyncMode.RefreshChannel,
        ["refreshChannelVideos"] = SyncMode.RefreshChannelVideos,
        ["downloadVideo"] = SyncMode.DownloadVideo,
        ["addChannel"] = SyncMode.AddChannel,
        ["youTubeSync"] = SyncMode.YouTubeSync,
        ["youTubeFullSync"] = SyncMode.YouTubeFullSync,
        ["retryDownloads"] = SyncMode.RetryDownloads,
        ["refreshAll"] = SyncMode.RefreshAll,
    };

    /// <summary>How a video's comments are listed (1.5, the watch page).</summary>
    public static readonly Table<CommentSort> CommentSorts = new()
    {
        ["top"] = CommentSort.Top,
        ["newest"] = CommentSort.Newest,
    };

    /// <summary>The states of a video or channel on YouTube (1.5, the admin pages' filter).</summary>
    public static readonly Table<YouTubeState> YouTubeStates = new()
    {
        ["notChecked"] = YouTubeState.NotChecked,
        ["onYouTube"] = YouTubeState.OnYouTube,
        ["unlisted"] = YouTubeState.Unlisted,
        ["private"] = YouTubeState.Private,
        ["retryLater"] = YouTubeState.RetryLater,
        ["gone"] = YouTubeState.Gone,
    };

    /// <summary>Which comments a refresh reads (1.5, the YouTube page).</summary>
    public static readonly Table<YouTubeCommentsChoice> CommentChoices = new()
    {
        ["off"] = YouTubeCommentsChoice.Off,
        ["top"] = YouTubeCommentsChoice.Top,
        ["all"] = YouTubeCommentsChoice.All,
    };

    /// <summary>An account's role (2.3.0).</summary>
    public static readonly Table<AccountRole> AccountRoles = new()
    {
        ["admin"] = AccountRole.Admin,
        ["user"] = AccountRole.User,
    };

    /// <summary>The LDAP page's tests (2.3.0), each on its own.</summary>
    public static readonly Table<LdapTestKind> LdapTestKinds = new()
    {
        ["connection"] = LdapTestKind.Connection,
        ["admins"] = LdapTestKind.Admins,
        ["users"] = LdapTestKind.Users,
        ["name"] = LdapTestKind.Name,
    };

    public static readonly Table<ApiKeyKind> ApiKeyKinds = new()
    {
        ["browserExtension"] = ApiKeyKind.BrowserExtension,
    };

    public static readonly Table<YtdlpChoiceKind> YtdlpChoices = new()
    {
        ["latest"] = YtdlpChoiceKind.Latest,
        ["official"] = YtdlpChoiceKind.Official,
        ["nightly"] = YtdlpChoiceKind.Nightly,
    };

    public static readonly Table<ImportPeriod> ImportPeriods = new()
    {
        ["day"] = ImportPeriod.Day,
        ["week"] = ImportPeriod.Week,
        ["month"] = ImportPeriod.Month,
        ["year"] = ImportPeriod.Year,
        ["all"] = ImportPeriod.All,
    };

    public static readonly Table<DashboardChannelSort> DashboardChannelSorts = new()
    {
        ["name"] = DashboardChannelSort.Name,
        ["videos"] = DashboardChannelSort.Videos,
        ["shorts"] = DashboardChannelSort.Shorts,
        ["streams"] = DashboardChannelSort.Streams,
        ["gone"] = DashboardChannelSort.Gone,
        ["private"] = DashboardChannelSort.Private,
        ["unlisted"] = DashboardChannelSort.Unlisted,
        ["retryLater"] = DashboardChannelSort.RetryLater,
        ["notChecked"] = DashboardChannelSort.NotChecked,
        ["total"] = DashboardChannelSort.Total,
        ["size"] = DashboardChannelSort.Size,
    };

    public static readonly Table<SortDirection> SortDirections = new()
    {
        ["asc"] = SortDirection.Ascending,
        ["desc"] = SortDirection.Descending,
    };

    public static readonly Table<DurationRange> Durations = new()
    {
        ["short"] = DurationRange.Short,
        ["medium"] = DurationRange.Medium,
        ["long"] = DurationRange.Long,
    };

    public static readonly Table<ChannelArtKind> ArtKinds = new()
    {
        ["thumb"] = ChannelArtKind.Thumb,
        ["banner"] = ChannelArtKind.Banner,
    };

    /// <summary>Parameter value → meaning, in the order the contract lists them (used in error messages).</summary>
    public sealed class Table<T>
        where T : struct, Enum
    {
        private readonly List<KeyValuePair<string, T>> _entries = [];

        public string Choices => string.Join(", ", _entries.Select(e => e.Key));

        public IReadOnlyList<string> Names => [.. _entries.Select(e => e.Key)];

        public T this[string name]
        {
            get => TryGet(name, out T value) ? value : throw new KeyNotFoundException(name);
            init => _entries.Add(new(name, value));
        }

        public bool TryGet(string? name, out T value)
        {
            foreach (KeyValuePair<string, T> entry in _entries)
            {
                if (string.Equals(entry.Key, name, StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public string NameOf(T value) => _entries.First(e => EqualityComparer<T>.Default.Equals(e.Value, value)).Key;
    }
}
