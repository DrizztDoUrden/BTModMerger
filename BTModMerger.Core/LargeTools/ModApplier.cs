using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core.LargeTools;

public class ModApplier(
    IApplier applier
)
    : IModApplier
{
    public (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files, IEnumerable<string> copies) Apply(
        XDocument basePackage,
        Func<string, Task<XDocument>> baseFiles,
        XDocument modDiff,
        Func<string, Task<XDocument>> modFiles)
    {
        var manifest = new XDocument(new XElement("ContentPackage"));

        var applied = ProcessFiles(basePackage, baseFiles, modDiff, modFiles)
            .Select(pair =>
            {
                var (path, task) = pair;

                return (path, data: task.ContinueWith(task =>
                {
                    var (record, data) = task.Result;
                    manifest.Root!.Add(record);
                    return data;
                }));
            })
            .ToArray();

        var copies = manifest
            .Elements(Elements.Copy)
            .Select(e => e.Attribute(Attributes.Path)?.Value)
            .Where(p => p is not null).Cast<string>()
            .ToArray();

        return (
            Task.WhenAll(applied.Select(p => p.data)).ContinueWith(_ => manifest),
            applied,
            copies
        );
    }

    internal IEnumerable<(string path, Task<(XElement record, XDocument data)>)> ProcessFiles(
        XDocument basePackage,
        Func<string, Task<XDocument>> baseFiles,
        XDocument modDiff,
        Func<string, Task<XDocument>> modFiles)
    {
        if (!basePackage.RootIs(Elements.ContentPackage))
            throw new InvalidDataException("Content package should have btmm:ContentPackage as root element");

        if (!modDiff.RootIs(Elements.ModDiff))
            throw new InvalidDataException("Mod diff should have btmm:ModDiff as root element");

        return modDiff.Root!.Elements()
            .Select(modElement =>
            {
                var name = modElement.Name;

                var filename = modElement.Attribute(Attributes.Path)?.Value
                    ?? throw new InvalidDataException($"Mod filelist has a child element <{name.Fancify()}> with no file attribute");

                return (filename, Task.Run(async () =>
                {
                    var file = await modFiles(filename);

                    var baseCPElement = basePackage.Root!.ElementsCIS(name).Single();
                    var basePath = baseCPElement.Attribute(Attributes.Path)?.Value
                        ?? throw new InvalidDataException($"Content package has a child element <{name.Fancify()}> with no btmm:Path attribute");
                    var baseFile = await baseFiles(basePath);
                    var ret = new XDocument();

                    applier.Apply(file.Root!, baseFile, ret, filename);

                    var record = new XElement(name, new XAttribute("file", $"%ModDir%/{filename}"));

                    return (record, ret);
                }));
            });
    }
}
