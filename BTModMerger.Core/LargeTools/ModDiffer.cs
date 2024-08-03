using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Utils;
using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core.LargeTools;

public class ModDiffer(
    IDiffer differ
)
{
    public IAsyncEnumerable<(string path, XElement record, XDocument data)> Apply(
        XDocument basePackage,
        Func<string, XDocument> baseFiles,
        XDocument modFilelist,
        Func<string, XDocument> modFiles,
        int threads,
        bool alwaysOverride)
    {
        if (!basePackage.RootIs(Elements.ContentPackage))
            throw new InvalidDataException("Content package should have btmm:ContentPackage as root element");

        if (!modFilelist.RootIsCIS("ContentPackage"))
            throw new InvalidDataException("Mod filelist should have ContentPackage as root element");

        return modFilelist.Root!.Elements()
            .AsParallelAsync(threads, (modElement, ct) => Task.Run(() =>
            {
                var name = modElement.Name;

                var filename = modElement.GetBTAttributeCIS("file")
                    ?? throw new InvalidDataException($"Mod filelist has a child element <{name.Fancify()}> with no file attribute");
                var file = modFiles(filename);

                var baseCPElement = basePackage.Root!.ElementsCIS(name).Single();
                var basePath = baseCPElement.Attribute(Attributes.Path)?.Value
                    ?? throw new InvalidDataException($"Content package has a child element <{name.Fancify()}> with no btmm:Path attribute");
                var baseFile = baseFiles(basePath);

                var ret = differ.Apply(baseFile, basePath, file, filename, alwaysOverride);

                var path = filename
                    .Replace(@"ModDir%/", "")
                    .Replace(@"ModDir%\", "");

                var record = new XElement(name, PathAttribute(path));

                return (path, record, ret);
            }));
    }
}
