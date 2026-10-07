using TonTuyau.Application.Store;

namespace TonTuyau.Application.UnitTests.Store;

/// <summary>QA10 M5-1: the last word, while still being typed, may be the start of a word.</summary>
public sealed class SearchTextTests
{
    [Theory]
    [InlineData("creme b", new[] { "creme" }, "b")]
    [InlineData("creme", new string[0], "creme")]
    [InlineData("creme ", new[] { "creme" }, null)]
    [InlineData("creme brulee   ", new[] { "creme", "brulee" }, null)]
    [InlineData("  creme   b", new[] { "creme" }, "b")]
    [InlineData("crème\tbrû", new[] { "crème" }, "brû")]
    [InlineData("creme ", new[] { "creme" }, null)]
    [InlineData("creme -", new[] { "creme" }, null)]
    [InlineData("creme - b", new[] { "creme" }, "b")]
    [InlineData("l'or", new string[0], "l'or")]
    [InlineData("a b c d", new[] { "a", "b", "c" }, "d")]
    public void The_last_word_is_a_prefix_unless_the_text_ends_with_a_space(
        string text,
        string[] words,
        string? prefix)
    {
        SearchText parsed = SearchText.Parse(text);

        Assert.Equal(words, parsed.Words);
        Assert.Equal(prefix, parsed.Prefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("- !")]
    [InlineData("…")]
    public void Nothing_searchable_is_empty(string text)
    {
        Assert.True(SearchText.Parse(text).IsEmpty);
    }

    [Fact]
    public void All_words_are_the_words_then_the_prefix()
    {
        Assert.Equal("creme brulee b", SearchText.Parse("creme  brulee b").AllWords);
        Assert.Equal("creme brulee", SearchText.Parse("creme brulee ").AllWords);
    }

    [Fact]
    public void Equal_texts_are_equal()
    {
        Assert.Equal(SearchText.Parse("creme b"), SearchText.Parse(" creme  b"));
        Assert.NotEqual(SearchText.Parse("creme b"), SearchText.Parse("creme b "));
    }
}
