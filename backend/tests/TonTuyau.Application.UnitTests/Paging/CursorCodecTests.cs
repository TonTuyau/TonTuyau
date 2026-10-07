using System.Text;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Paging;

public sealed class CursorCodecTests
{
    private static readonly string Fingerprint = CursorCodec.Fingerprint("videos|latest");

    public enum Kind
    {
        Text,
        Whole,
        Real,
    }

    [Theory, CombinatorialData]
    public void A_position_comes_back_from_its_cursor(
        [CombinatorialValues(Kind.Text, Kind.Whole, Kind.Real)] Kind first,
        [CombinatorialValues(Kind.Text, Kind.Whole, Kind.Real)] Kind second,
        [CombinatorialValues(0, 1, 2)] int variant)
    {
        SortPosition position = new([Value(first, variant), Value(second, variant + 1)]);

        string cursor = CursorCodec.Encode(Fingerprint, position);
        PageCursor? decoded = CursorCodec.Decode(cursor, Fingerprint);

        Assert.NotNull(decoded);
        Assert.Equal(position.Values, decoded.After.Values);
        Assert.Null(decoded.Since);
    }

    [Theory, CombinatorialData]
    public void A_frozen_cut_off_comes_back_to_the_millisecond(
        [CombinatorialValues(0L, 1_759_436_328_123L, -62_135_596_800_000L)] long milliseconds)
    {
        DateTimeOffset since = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);

        string cursor = CursorCodec.Encode(Fingerprint, new([new IntegerSortValue(1)]), since);

        Assert.Equal(since, CursorCodec.Decode(cursor, Fingerprint)!.Since);
    }

    [Fact]
    public void A_cursor_is_url_safe()
    {
        string cursor = CursorCodec.Encode(Fingerprint, new([new TextSortValue("é/+?&= ü"), new RealSortValue(1.5)]));

        Assert.All(cursor, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_', $"'{c}'"));
    }

    [Fact]
    public void No_cursor_means_the_first_page() => Assert.Null(CursorCodec.Decode(null, Fingerprint));

    [Fact]
    public void A_cursor_made_for_another_query_is_refused()
    {
        string cursor = CursorCodec.Encode(Fingerprint, new([new IntegerSortValue(1)]));

        ValidationException error = Assert.Throws<ValidationException>(
            () => CursorCodec.Decode(cursor, CursorCodec.Fingerprint("videos|popular")));

        ValidationError only = Assert.Single(error.Errors);
        Assert.Equal(("cursor", CursorCodec.OtherQuery), (only.Field, only.Message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("!!!!")]
    [InlineData("e30")] // {}
    [InlineData("W10")] // []
    [InlineData("eyJmIjoxfQ")] // {"f":1}
    [InlineData("eyJmIjoiYSJ9")] // {"f":"a"} (no position)
    [InlineData("eyJmIjoiYSIsImEiOltdfQ")] // {"f":"a","a":[]}
    [InlineData("eyJmIjoiYSIsImEiOlsxXX0")] // {"f":"a","a":[1]} (not a typed value)
    [InlineData("eyJmIjoiYSIsImEiOlt7InMiOjF9XX0")] // {"f":"a","a":[{"s":1}]} (text that isn't text)
    [InlineData("eyJmIjoiYSIsImEiOlt7InMiOiJ4IiwiaSI6MX1dfQ")] // two values in one
    [InlineData("eyJmIjoiYSIsImEiOlt7InEiOjF9XX0")] // unknown kind
    [InlineData("eyJmIjoiYSIsImEiOlt7ImkiOjEuNX1dfQ")] // {"i":1.5}: an integer that isn't one
    [InlineData("eyJmIjo")] // truncated JSON
    [InlineData("eyJmIjoiYSIsImEiOlt7InMiOiJ4In1dLCJ0IjoieCJ9")] // a cut-off that isn't a number
    [InlineData("eyJmIjoiYSIsImEiOlt7InMiOiJ4In1dLCJ0Ijo5OTk5OTk5OTk5OTk5OTk5OTk5fQ")] // a cut-off past year 9999
    public void Anything_that_isnt_one_of_our_cursors_is_refused(string cursor)
    {
        ValidationException error = Assert.Throws<ValidationException>(() => CursorCodec.Decode(cursor, "a"));

        Assert.Equal(("cursor", CursorCodec.NotACursor), (error.Errors[0].Field, error.Errors[0].Message));
    }

    [Fact]
    public void A_hand_made_cursor_in_the_right_format_is_read()
    {
        // The control for the cases above: {"f":"a","a":[{"s":"x"}]}.
        PageCursor? cursor = CursorCodec.Decode("eyJmIjoiYSIsImEiOlt7InMiOiJ4In1dfQ", "a");

        Assert.Equal([new TextSortValue("x")], cursor!.After.Values);
    }

    [Fact]
    public void An_oversized_cursor_is_refused_without_being_decoded()
    {
        string huge = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', CursorCodec.MaxLength)));

        Assert.Throws<ValidationException>(() => CursorCodec.Decode(huge, Fingerprint));
    }

    [Theory, CombinatorialData]
    public void Fingerprints_are_short_stable_and_distinct([CombinatorialValues("a", "b", "videos|x")] string query)
    {
        Assert.Equal(CursorCodec.Fingerprint(query), CursorCodec.Fingerprint(query));
        Assert.Equal(16, CursorCodec.Fingerprint(query).Length);
        Assert.NotEqual(CursorCodec.Fingerprint(query), CursorCodec.Fingerprint(query + "!"));
    }

    private static SortValue Value(Kind kind, int variant) => kind switch
    {
        Kind.Text => new TextSortValue(((string[])["", "UCé_x-y", "\"quoted\" \\ text"])[variant % 3]),
        Kind.Whole => new IntegerSortValue(((long[])[0, long.MinValue, long.MaxValue])[variant % 3]),
        _ => new RealSortValue(((double[])[0.0, 1.0 / 3, 12345.678e10])[variant % 3]),
    };
}
