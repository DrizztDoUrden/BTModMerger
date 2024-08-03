using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core.LargeTools;

public class ModDiffer(
    IDiffer differ
)
    : IModDiffer
{
    public (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files) Apply(
        XDocument basePackage,
        Func<string, Task<XDocument>> baseFiles,
        XDocument modFilelist,
        Func<string, Task<XDocument>> modFiles,
        IEnumerable<string> allModXmlFiles,
        bool alwaysOverride)
    {
        var manifest = new XDocument(ModDiff());
        var files = new List<(string path, Task<XDocument> data)>();
        var listedFiles = new HashSet<string>();

        foreach (var (path, task) in ProcessFiles(basePackage, baseFiles, modFilelist, modFiles, alwaysOverride))
        {
            listedFiles.Add(path);
            files.Add((path, task.ContinueWith(task =>
            {
                var (record, data) = task.Result;
                manifest.Root!.Add(record);
                return data;
            })));
        }

        foreach (var path in allModXmlFiles.Where(path => !listedFiles.Contains(path)))
            manifest.Root!.Add(Copy(path));

        return (
            Task.WhenAll(files.Select(p => p.data).ToArray()).ContinueWith(_ => manifest),
            files
        );
    }

    internal IEnumerable<(string path, Task<(XElement record, XDocument data)>)> ProcessFiles(
        XDocument basePackage,
        Func<string, Task<XDocument>> baseFiles,
        XDocument modFilelist,
        Func<string, Task<XDocument>> modFiles,
        bool alwaysOverride)
    {
        if (!basePackage.RootIs(Elements.ContentPackage))
            throw new InvalidDataException("Content package should have btmm:ContentPackage as root element");

        if (!modFilelist.RootIsCIS("ContentPackage"))
            throw new InvalidDataException("Mod filelist should have ContentPackage as root element");

        return modFilelist.Root!.Elements()
            .Select(modElement =>
            {
                var name = modElement.Name;

                var filename = modElement.GetBTAttributeCIS("file")
                    ?? throw new InvalidDataException($"Mod filelist has a child element <{name.Fancify()}> with no file attribute");

                var path = filename
                    .Replace(@"ModDir%/", "")
                    .Replace(@"ModDir%\", "");

                return (path, Task.Run(async () =>
                {
                    var file = await modFiles(filename);

                    var baseCPElement = basePackage.Root!.ElementsCIS(name).Single();
                    var basePath = baseCPElement.Attribute(Attributes.Path)?.Value
                        ?? throw new InvalidDataException($"Content package has a child element <{name.Fancify()}> with no btmm:Path attribute");
                    var baseFile = await baseFiles(basePath);

                    var ret = differ.Apply(baseFile, basePath, file, filename, alwaysOverride);

                    var record = new XElement(name,
                        PathAttribute(path),
                        BaseAttribute(basePath)
                    );

                    return (record, ret);
                }));
            });
    }
}
