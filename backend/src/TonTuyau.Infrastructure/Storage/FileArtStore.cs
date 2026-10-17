using System.Globalization;
using SkiaSharp;
using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary>
/// Art under the art root, keeping TubeArchivist's layout without the <c>/cache</c> prefix:
/// <c>/cache/videos/a/abcdefghijk.jpg</c> → <c>&lt;root&gt;/videos/a/abcdefghijk.jpg</c>, with its resized WebP
/// versions beside it (<c>abcdefghijk.360.webp</c>, QA09).
/// </summary>
internal sealed class FileArtStore(string root) : IArtStore
{
    private const string VersionExtension = ".webp";

    private readonly string _root = Path.GetFullPath(root);

    public bool Exists(string cachePath) => File.Exists(FileFor(cachePath));

    public async Task SaveAsync(string cachePath, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        string file = FileFor(cachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temp = TempFor(file);
        try
        {
            await using (FileStream output = File.Create(temp))
            {
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public void Delete(string cachePath)
    {
        string file = FileFor(cachePath);
        // Checked first: File.Delete throws when the folder itself is missing (a picture never saved there).
        string directory = Path.GetDirectoryName(file)!;
        if (!Directory.Exists(directory))
        {
            return;
        }

        File.Delete(file);

        string stem = Path.GetFileNameWithoutExtension(file);
        foreach (string version in Directory.EnumerateFiles(directory, stem + ".*" + VersionExtension))
        {
            string width = Path.GetFileName(version)[(stem.Length + 1)..^VersionExtension.Length];
            if (width.Length > 0 && width.All(char.IsAsciiDigit))
            {
                File.Delete(version);
            }
        }
    }

    public StoredFile? Find(string cachePath) => StoredFiles.Find(FileFor(cachePath));

    public StoredFile? FindVersion(string cachePath, int width) => StoredFiles.Find(VersionFor(cachePath, width));

    public ArtVersions? Describe(string cachePath, IReadOnlyList<int> widths)
    {
        ArgumentNullException.ThrowIfNull(widths);
        string file = FileFor(cachePath);
        if (!File.Exists(file))
        {
            return null;
        }

        // The header only: the image isn't decoded.
        using SKCodec? codec = SKCodec.Create(file);
        if (codec is null)
        {
            return null;
        }

        int width = codec.Info.Width;
        int[] existing = ArtSizes
            .Targets(width, widths)
            .Where(w => File.Exists(VersionFor(cachePath, w)))
            .ToArray();
        return new ArtVersions(width, existing);
    }

    /// <summary>Writes the bytes to a temp file next to the target, then renames it: never a half-written image.</summary>
    internal static void WriteAtomically(string file, ReadOnlySpan<byte> content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temp = TempFor(file);
        try
        {
            using (FileStream output = File.Create(temp))
            {
                output.Write(content);
            }

            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>
    /// A resized version sits next to its original: <c>videos/a/abcdefghijk.jpg</c> → <c>videos/a/abcdefghijk.360.webp</c>.
    /// </summary>
    internal string VersionFor(string cachePath, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        string file = FileFor(cachePath);
        return Path.ChangeExtension(file, null) + "." + width.ToString(CultureInfo.InvariantCulture) + VersionExtension;
    }

    internal string FileFor(string cachePath)
    {
        if (!LibraryPaths.IsArtPath(cachePath))
        {
            throw new ArgumentException($"'{cachePath}' isn't a TubeArchivist art path.", nameof(cachePath));
        }

        string file = Path.GetFullPath(Path.Combine(_root, cachePath["/cache/".Length..]));

        // Belt and braces: the format check already rules out "..", this rules out anything it might miss.
        return file.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? file
            : throw new ArgumentException($"'{cachePath}' resolves outside the art root.", nameof(cachePath));
    }

    private static string TempFor(string file) => file + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
}
