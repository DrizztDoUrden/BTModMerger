using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Utils;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core.LargeTools;

public class ContentPackageFuser(
    IFuser fuser
)
    : IContentPackageFuser
{
    public IAsyncEnumerable<(string path, XElement record, XDocument data)> Apply(XDocument contentPackage, Func<string, XDocument> fileGetters, int threads)
    {
        if (!contentPackage.RootIsCIS("ContentPackage"))
            throw new InvalidDataException("Content package should have ContentPackage as root element");

        return contentPackage.Root!.Elements()
            .GroupBy(e => e.Name)
            .AsParallelAsync(threads, (items, ct) => Task.Run(() =>
            {
                var name = items.Key;
                var ret = new XDocument(FusedBase());
                var path = $"{name.Fancify()}.xml";
                var record = new XElement(name, PathAttribute(path));

                foreach (var file in items)
                {
                    var filename = file.GetBTAttributeCIS("file")
                        ?? throw new InvalidDataException("ContentPackage has a child element with no file attribute");
                    var part = fileGetters(filename);

                    fuser.Apply(ret.Root!, part.Root!, name.Fancify(), filename);
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

                return (path, record, ret);
            }));
    }
}
