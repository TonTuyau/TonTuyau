using TonTuyau.Application.Library;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Browse;

/// <summary>Rules shared by the request validators. Field names are the query parameters' names.</summary>
public static class RequestRules
{
    public const string Subject = "request";

    public static void Size(ValidationErrors errors, int? size, int max)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (size is not null && (size.Value < 1 || size.Value > max))
        {
            errors.Add("size", $"must be between 1 and {max}.");
        }
    }

    public static void OneOf<T>(ValidationErrors errors, string field, string? value, ApiValues.Table<T> table)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(table);
        if (value is not null && !table.TryGet(value, out _))
        {
            errors.Add(field, $"must be one of {table.Choices}.");
        }
    }

    /// <summary>Free text, trimmed: 1 to <paramref name="max"/> characters. Optional text left blank = not given.</summary>
    public static void Text(ValidationErrors errors, string field, string? value, bool required, int max)
    {
        ArgumentNullException.ThrowIfNull(errors);
        string? trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (required)
            {
                errors.Add(field, "is required.");
            }
        }
        else if (trimmed.Length > max)
        {
            errors.Add(field, $"must be at most {max} characters.");
        }
    }

    public static void VideoId(ValidationErrors errors, string field, string? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is not null && !LibraryIds.IsVideoId(value))
        {
            errors.Add(field, "isn't a video id.");
        }
    }

    /// <summary>The parameters every plain video list takes (<c>/api/videos</c> and a channel's tabs).</summary>
    public static void VideoList(ValidationErrors errors, string? type, string? sort, string? exclude, int? size)
    {
        OneOf(errors, "type", type, ApiValues.Types);
        OneOf(errors, "sort", sort, ApiValues.VideoSorts);
        VideoId(errors, "exclude", exclude);
        Size(errors, size, ApiValues.MaxPageSize);
    }

    public static void ChannelId(ValidationErrors errors, string field, string? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is not null && !LibraryIds.IsChannelId(value))
        {
            errors.Add(field, "isn't a channel id.");
        }
    }

    /// <exception cref="ValidationException">Not a video id (e.g. a path traversal attempt).</exception>
    public static void RequireVideoId(string? id)
    {
        if (!LibraryIds.IsVideoId(id))
        {
            throw Invalid("id", "isn't a video id.");
        }
    }

    /// <exception cref="ValidationException">Not a channel id (e.g. a path traversal attempt).</exception>
    public static void RequireChannelId(string? id)
    {
        if (!LibraryIds.IsChannelId(id))
        {
            throw Invalid("id", "isn't a channel id.");
        }
    }

    public static ValidationException Invalid(string field, string message)
    {
        ValidationError error = new(field, message);
        return new ValidationException(Subject, [error]);
    }
}

public sealed class HomeRequestValidator : IValidator<HomeRequest>
{
    public void Validate(HomeRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.Size(errors, value.Size, ApiValues.MaxPageSize);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}

public sealed class VideoListRequestValidator : IValidator<VideoListRequest>
{
    public void Validate(VideoListRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.VideoList(errors, value.Type, value.Sort, value.Exclude, value.Size);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}

public sealed class ChannelVideosRequestValidator : IValidator<ChannelVideosRequest>
{
    public void Validate(ChannelVideosRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequestRules.RequireChannelId(value.ChannelId);
        ValidationErrors errors = new();
        RequestRules.VideoList(errors, value.Type, value.Sort, value.Exclude, value.Size);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}

public sealed class ChannelListRequestValidator : IValidator<ChannelListRequest>
{
    public void Validate(ChannelListRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.Text(errors, "q", value.Q, required: false, ApiValues.MaxChannelFilterLength);
        RequestRules.OneOf(errors, "sort", value.Sort, ApiValues.ChannelSorts);
        RequestRules.Size(errors, value.Size, ApiValues.MaxPageSize);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}

public sealed class VideoSearchRequestValidator : IValidator<VideoSearchRequest>
{
    public void Validate(VideoSearchRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.Text(errors, "q", value.Q, required: true, ApiValues.MaxSearchLength);
        RequestRules.OneOf(errors, "kind", value.Kind, ApiValues.Kinds);
        RequestRules.OneOf(errors, "type", value.Type, ApiValues.Types);
        RequestRules.ChannelId(errors, "channel", value.Channel);
        RequestRules.OneOf(errors, "published", value.Published, ApiValues.Periods);
        RequestRules.OneOf(errors, "duration", value.Duration, ApiValues.Durations);
        RequestRules.OneOf(errors, "sort", value.Sort, ApiValues.SearchSorts);
        RequestRules.Size(errors, value.Size, ApiValues.MaxPageSize);


        if (value.Kind == ApiValues.Kinds.NameOf(SearchKind.Channels))
        {
            // Channel results have no type, date or duration: a filter the results can't honour is a mistake.
            foreach ((string field, string? given) in VideoOnly(value))
            {
                if (given is not null)
                {
                    errors.Add(field, "only applies to videos (kind all or videos).");
                }
            }

            if (value.Sort is not null && value.Sort != ApiValues.SearchSorts.NameOf(Store.VideoListSort.Relevance))
            {
                errors.Add("sort", "channels are only sorted by relevance.");
            }
        }

        errors.ThrowIfAny(RequestRules.Subject);
    }

    private static (string, string?)[] VideoOnly(VideoSearchRequest value) =>
    [
        ("type", value.Type),
        ("channel", value.Channel),
        ("published", value.Published),
        ("duration", value.Duration),
    ];
}

public sealed class SuggestRequestValidator : IValidator<SuggestRequest>
{
    public void Validate(SuggestRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.Text(errors, "q", value.Q, required: true, ApiValues.MaxSuggestLength);
        RequestRules.Size(errors, value.Size, ApiValues.MaxSuggestSize);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}
