using TonTuyau.Application.Media;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests.Media;

public sealed class ByteRangesTests
{
    private static readonly StoredFile File = new(
        "/media/a.mp4",
        1000,
        new DateTimeOffset(2026, 10, 1, 18, 23, 25, 123, TimeSpan.Zero));

    [Theory]
    [InlineData("bytes=0-99", 0, 99)]
    [InlineData("bytes=0-0", 0, 0)]
    [InlineData("bytes=999-999", 999, 999)]
    [InlineData("bytes=500-", 500, 999)]
    [InlineData("bytes=0-", 0, 999)]
    [InlineData("bytes=-1", 999, 999)]
    [InlineData("bytes=-10", 990, 999)]
    [InlineData("bytes=-1000", 0, 999)]
    [InlineData("bytes=-5000", 0, 999)]
    [InlineData("bytes=900-5000", 900, 999)]
    [InlineData("bytes=900-99999999999999999999", 900, 999)]
    [InlineData("bytes=-99999999999999999999", 0, 999)]
    [InlineData("BYTES=1-2", 1, 2)]
    [InlineData("  bytes=1-2  ", 1, 2)]
    [InlineData("bytes= 1 - 2 ", 1, 2)]
    [InlineData("bytes=1-2,", 1, 2)]
    public void One_range_is_served(string header, long start, long end)
    {
        ByteRange? range = ByteRanges.Parse(header, 1000);

        Assert.Equal(new ByteRange(start, end), range);
        Assert.Equal(end - start + 1, range!.Length);
        Assert.Equal($"bytes {start}-{end}/1000", range.ContentRange(1000));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bytes=")]
    [InlineData("bytes=-")]
    [InlineData("bytes=abc")]
    [InlineData("bytes=1-a")]
    [InlineData("bytes=+1-2")]
    [InlineData("bytes=1--2")]
    [InlineData("bytes=50-10")]
    [InlineData("bytes=0-9,20-29")]
    [InlineData("bytes=0-9, 0-9")]
    [InlineData("items=0-9")]
    [InlineData("bytes 0-9")]
    [InlineData("bytes=٠-٩")]
    public void What_the_RFC_lets_a_server_ignore_gets_the_whole_file(string? header) =>
        Assert.Null(ByteRanges.Parse(header, 1000));

    [Theory, CombinatorialData]
    public void A_range_starting_past_the_end_is_unsatisfiable(
        [CombinatorialValues(
            "bytes=1000-",
            "bytes=1000-2000",
            "bytes=5000-",
            "bytes=-0",
            "bytes=99999999999999999999-")]
        string header)
    {
        RangeNotSatisfiableException error = Assert.Throws<RangeNotSatisfiableException>(
            () => ByteRanges.Parse(header, 1000));

        Assert.Equal("bytes */1000", error.ContentRange);
    }

    [Theory, CombinatorialData]
    public void Nothing_can_be_cut_from_an_empty_file(
        [CombinatorialValues("bytes=0-", "bytes=0-0", "bytes=-1", "bytes=-0")] string header) =>
        Assert.Throws<RangeNotSatisfiableException>(() => ByteRanges.Parse(header, 0));

    [Theory, CombinatorialData]
    public void Ranges_stay_inside_files_of_any_size(
        [CombinatorialValues(1, 2, 999, 1000, 1001)] long length,
        [CombinatorialValues(0, 1, 500, 998, 999)] long start,
        [CombinatorialValues(null, 0L, 1L, 999L, 1000L, 5000L)] long? suffixOrEnd)
    {
        string header = suffixOrEnd is { } end ? $"bytes={start}-{Math.Max(start, end)}" : $"bytes={start}-";
        if (start >= length)
        {
            Assert.Throws<RangeNotSatisfiableException>(() => ByteRanges.Parse(header, length));
            return;
        }

        ByteRange range = ByteRanges.Parse(header, length)!;

        Assert.Equal(start, range.Start);
        Assert.InRange(range.End, range.Start, length - 1);
    }

    [Fact]
    public void The_ETag_changes_with_the_size_and_the_date()
    {
        string etag = ByteRanges.ETag(File);

        Assert.StartsWith("\"", etag, StringComparison.Ordinal);
        Assert.NotEqual(etag, ByteRanges.ETag(File with { Length = 1001 }));
        Assert.NotEqual(etag, ByteRanges.ETag(File with { LastModified = File.LastModified.AddTicks(1) }));
    }

    [Theory]
    [InlineData("etag")]
    [InlineData("Thu, 01 Oct 2026 18:23:25 GMT")]
    [InlineData(null)]
    public void An_If_Range_naming_the_current_version_keeps_the_range(string? ifRange)
    {
        string? header = ifRange == "etag" ? ByteRanges.ETag(File) : ifRange;

        ByteRange? range = ByteRanges.Select("bytes=0-9", header, File);

        Assert.Equal(new ByteRange(0, 9), range);
    }

    [Theory]
    [InlineData("\"other\"")]
    [InlineData("W/\"0-0\"")]
    [InlineData("Thu, 01 Oct 2026 18:23:24 GMT")]
    [InlineData("not a date")]
    public void An_If_Range_naming_another_version_gets_the_whole_file(string ifRange) =>
        Assert.Null(ByteRanges.Select("bytes=0-9", ifRange, File));

    [Fact]
    public void A_weak_ETag_never_keeps_the_range() =>
        Assert.Null(ByteRanges.Select("bytes=0-9", "W/" + ByteRanges.ETag(File), File));

    [Fact]
    public void Without_a_range_the_whole_file_is_sent_whatever_If_Range_says() =>
        Assert.Null(ByteRanges.Select(null, "\"other\"", File));
}
