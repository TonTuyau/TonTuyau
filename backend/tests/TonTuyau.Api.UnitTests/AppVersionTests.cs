using System.Text.RegularExpressions;
using System.Xml.Linq;
using TonTuyau.Api.Hosting;

namespace TonTuyau.Api.UnitTests;

public sealed partial class AppVersionTests
{
    [Fact]
    public void The_version_is_the_one_in_Directory_Build_props_without_a_commit_suffix()
    {
        string? expected = VersionInBuildProps();

        Assert.NotNull(expected);
        Assert.Matches(SemVer(), expected);
        Assert.Equal(expected, AppVersion.Value);
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-(alpha|beta|rc)\.\d+)?\z")]
    private static partial Regex SemVer();

    /// <summary>The first <c>Version</c> in the Directory.Build.props files above the test (tests/ has its own).</summary>
    private static string? VersionInBuildProps()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string props = Path.Combine(dir.FullName, "Directory.Build.props");
            string? version = File.Exists(props)
                ? XDocument
                    .Load(props)
                    .Descendants("Version")
                    .Select(e => e.Value.Trim())
                    .SingleOrDefault()
                : null;
            if (version is not null)
            {
                return version;
            }
        }

        return null;
    }
}
