using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IFileIO
{
    Stream OpenStandardInputStream();
    Stream OpenStandardOutputStream();
    Stream OpenReadStream(string path);
    Stream OpenWriteStream(string path);
    bool FileExists(string path);
    void DeleteFile(string path);
    bool IsDirectory(string path);
    DateTime GetLastWriteTimeUtc(string path);
    IEnumerable<string> GetFiles(string path, string pattern = "*", SearchOption options = SearchOption.TopDirectoryOnly);

    public sealed XmlReader OpenStandardInput() => XmlReader.Create(OpenStandardInputStream(), new()
    {
        CloseInput = false,
    });

    public sealed XmlReader OpenRead(string path) => XmlReader.Create(OpenReadStream(path), new()
    {
        CloseInput = true,
    });

    public sealed XmlWriter OpenStandardOutput() => XmlWriter.Create(OpenStandardOutputStream(), new()
    {
        CloseOutput = false,
        NewLineOnAttributes = false,
        Indent = true,
    });

    public sealed XmlWriter OpenWrite(string path) => XmlWriter.Create(OpenWriteStream(path), new()
    {
        CloseOutput = true,
        NewLineOnAttributes = false,
        Indent = true,
    });

    public sealed XDocument OpenInput([NotNull] ref string? inputPath)
    {
        var ret = string.IsNullOrWhiteSpace(inputPath)
            ? OpenInput()
            : OpenInput(inputPath);

        inputPath ??= "cin";
        return ret;
    }

    public sealed XDocument OpenInput(string inputPath)
    {
        using var baseFile = OpenRead(inputPath);
        return XDocument.Load(baseFile, LoadOptions.None);
    }

    public sealed XDocument OpenInput()
    {
        using var baseFile = OpenStandardInput();
        return XDocument.Load(baseFile, LoadOptions.None);
    }

    public sealed async Task<XDocument> OpenInputAsync(string inputPath, CancellationToken? ct = null)
    {
        using var baseFile = OpenRead(inputPath);
        return await XDocument.LoadAsync(baseFile, LoadOptions.None, ct ?? CancellationToken.None);
    }

    public sealed async Task<XDocument> OpenInputAsync(CancellationToken? ct = null)
    {
        using var baseFile = OpenStandardInput();
        return await XDocument.LoadAsync(baseFile, LoadOptions.None, ct ?? CancellationToken.None);
    }

    public sealed void SaveResult(string? outputPath, XDocument? result, bool force = false)
    {
        if (result is null || !force && !result.Elements().Any())
            return;

        using var writer = string.IsNullOrWhiteSpace(outputPath)
            ? OpenStandardOutput()
            : OpenWrite(outputPath);

        result.Save(writer);
    }

    public sealed async Task SaveResultAsync(string? outputPath, XDocument? result, CancellationToken? ct = null, bool force = false)
    {
        if (result is null || !force && !result.Elements().Any())
            return;

        using var writer = string.IsNullOrWhiteSpace(outputPath)
            ? OpenStandardOutput()
            : OpenWrite(outputPath);

        await result.SaveAsync(writer, ct ?? CancellationToken.None);
    }

    public sealed async Task<(XDocument manifest, Func<string, Task<XDocument>> files)> OpenBTMMPackage(string path, string defaultPackageName, CancellationToken? ct = null)
    {
        string directory;

        if (IsDirectory(path))
        {
            directory = path;
            path = Path.Combine(path, defaultPackageName);
        }
        else
        {
            directory = Path.GetDirectoryName(path)!;
        }

        ct ??= CancellationToken.None;

        return (await OpenInputAsync(path, ct), async filename =>
        {
            var cleanFilename = filename
                .Replace(@"ModDir%/", "")
                .Replace(@"ModDir%\", "");

            return await OpenInputAsync(Path.Combine(directory, cleanFilename), ct);
        });
    }
}
