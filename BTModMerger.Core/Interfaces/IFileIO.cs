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
    bool FileExists(string outputPath);
    void DeleteFile(string outputPath);

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

    public sealed void SaveResult(string? outputPath, XDocument? result)
    {
        if (result == null || !result.Elements().Any())
            return;

        using var writer = string.IsNullOrWhiteSpace(outputPath)
            ? OpenStandardOutput()
            : OpenWrite(outputPath);

        result.Save(writer);
    }
}
