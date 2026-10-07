using TonTuyau.Application.Media;

namespace TonTuyau.Application.UnitTests.Media;

public sealed class DownloadFileNameTests
{
    [Theory, CombinatorialData]
    public void Characters_a_file_name_cant_hold_are_replaced(
        [CombinatorialValues('\\', '/', ':', '*', '?', '"', '<', '>', '|')] char forbidden,
        [CombinatorialValues("mp4", "webm")] string extension)
    {
        DownloadName name = DownloadFileName.For($"Avant {forbidden} après", extension);

        Assert.Equal($"Avant _ après.{extension}", name.Unicode);
        Assert.Equal($"Avant _ apres.{extension}", name.Ascii);
    }

    [Theory, CombinatorialData]
    public void Control_characters_and_odd_spaces_become_one_space(
        [CombinatorialValues("\t", "\n", "\r\n", "\u0001", "\u007f", " ", "  ", " \t ")] string space)
    {
        DownloadName name = DownloadFileName.For($"Un{space}deux", "mp4");

        Assert.Equal("Un deux.mp4", name.Unicode);
    }

    [Theory]
    [InlineData("Titre.", "Titre")]
    [InlineData("Titre...", "Titre")]
    [InlineData("  Titre  ", "Titre")]
    [InlineData("Titre . . ", "Titre")]
    [InlineData("", "video")]
    [InlineData("   ", "video")]
    [InlineData("...", "video")]
    [InlineData("CON", "CON_")]
    [InlineData("nul", "nul_")]
    [InlineData("Com1", "Com1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("CONSOLE", "CONSOLE")]
    public void Names_Windows_refuses_are_avoided(string title, string expected) =>
        Assert.Equal(expected, DownloadFileName.SafeTitle(title));

    [Theory]
    [InlineData("Crème brûlée", "Creme brulee")]
    [InlineData("Œuvre complète", "OEuvre complete")]
    [InlineData("L’huile d’olive", "L'huile d'olive")]
    [InlineData("Pâte — étape… 2", "Pate - etape... 2")]
    [InlineData("Et après…", "Et apres")]
    [InlineData("100% bio", "100_ bio")]
    [InlineData("日本語", "___")]
    [InlineData("Emoji 🎉 fête", "Emoji _ fete")]
    public void The_plain_name_is_ASCII(string title, string ascii)
    {
        DownloadName name = DownloadFileName.For(title, "mp4");

        Assert.Equal(ascii + ".mp4", name.Ascii);
        Assert.All(name.Ascii, c => Assert.True(char.IsAscii(c)));
    }

    [Fact]
    public void The_real_title_is_kept_in_the_UTF_8_name()
    {
        DownloadName name = DownloadFileName.For("HUILES : la cuisson 🍳 (et d'autres)", "mp4");

        Assert.Equal("HUILES _ la cuisson 🍳 (et d'autres).mp4", name.Unicode);
        Assert.Equal(
            "attachment; filename=\"HUILES _ la cuisson _ (et d'autres).mp4\"; "
                + "filename*=UTF-8''HUILES%20_%20la%20cuisson%20%F0%9F%8D%B3%20%28et%20d%27autres%29.mp4",
            name.ContentDisposition);
    }

    [Theory, CombinatorialData]
    public void Long_titles_are_cut_without_splitting_a_character(
        [CombinatorialValues("a", "é", "🎉", "é")] string character,
        [CombinatorialValues(149, 150, 151, 400)] int count)
    {
        string title = string.Concat(Enumerable.Repeat(character, count));

        string name = DownloadFileName.SafeTitle(title);

        int kept = Math.Min(count, DownloadFileName.MaxTitleLength);
        Assert.Equal(
            string.Concat(Enumerable.Repeat(character, kept)),
            name);
    }

    [Theory]
    [InlineData("Très long titre ", 149)]
    [InlineData("Titre.", 145)]
    public void A_cut_never_ends_on_a_space_or_a_dot(string end, int padding)
    {
        string name = DownloadFileName.SafeTitle(new string('x', padding) + end + "suite");

        Assert.False(name.EndsWith(' ') || name.EndsWith('.'), name);
    }
}
