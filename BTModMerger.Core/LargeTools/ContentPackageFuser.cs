using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core.LargeTools;

public class ContentPackageFuser(
    IFuser fuser
)
    : IContentPackageFuser
{
    public (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files) Apply(XDocument contentPackage, Func<string, Task<XDocument>> fileGetters)
    {
        var manifest = new XDocument(ContentPackage());
        var files = new List<(string path, Task<XDocument> data)>();

        foreach (var (path, task) in ProcessFiles(contentPackage, fileGetters))
        {
            files.Add((path, Task.Run(async() =>
            {
                var (record, data) = await task;
                manifest.Root!.Add(record);
                return data;
            })));
        }

        return (
            Task.WhenAll(files.Select(p => p.data).ToArray()).ContinueWith(_ => manifest),
            files
        );
    }

    internal IEnumerable<(string path, Task<(XElement record, XDocument data)>)> ProcessFiles(XDocument contentPackage, Func<string, Task<XDocument>> fileGetters)
    {
        if (!contentPackage.RootIsCIS("ContentPackage"))
            throw new InvalidDataException("Content package should have ContentPackage as root element");

        return contentPackage.Root!.Elements()
            .Where(e => e.GetBTAttributeCIS("file")?.EndsWith(".xml") ?? throw new InvalidDataException($"Some weird shit in without file attribute."))
            .GroupBy(e => e.Name)
            .Select(items =>
            {
                var name = items.Key;
                var path = $"{name.Fancify()}.xml";

                return (path, Task.Run(async () =>
                {
                    var ret = new XDocument(FusedBase());
                    var record = new XElement(name, PathAttribute(path));

                    foreach (var file in items)
                    {
                        var filename = file.GetBTAttributeCIS("file")
                            ?? throw new InvalidDataException("ContentPackage has a child element with no file attribute");
                        var data = await fileGetters(filename);

                        fuser.Apply(ret.Root!, data.Root!, name.Fancify(), filename);
                        record.Add(Part(filename));
                    }

                    if (ret.Root!.Elements().All(e => e.Name == name && !e.HasAttributes))
                    {
                        var newRoot = new XElement(name);
                        foreach (var child in ret.Root.Elements())
                            foreach (var subChild in child.Elements())
                                newRoot.Add(subChild);
                        ret = new XDocument(newRoot);
                    }

                    return (record, ret);
                }));
            });
    }
}
