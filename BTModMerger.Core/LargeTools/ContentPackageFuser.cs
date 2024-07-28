using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core.LargeTools;

public class ContentPackageFuser(
    IFuser fuser
)
{
    public IEnumerable<Task<(string path, XDocument data)>> Apply(XDocument contentPackage, Func<string, XDocument> fileGetters)
    {
        if (contentPackage.Root is null)
            throw new InvalidDataException("Content package is empty");

        if (!contentPackage.Root.IsNameEqualCIS("ContentPackage"))
            throw new InvalidDataException("Content package should have ContentPackage as root element");

        foreach (var items in contentPackage.Root.Elements().GroupBy(e => e.Name))
        {
            var task = new Task<(string, XDocument)>(() =>
            {
                var name = items.Key;
                var ret = new XDocument(FusedBase());

                foreach (var file in items)
                {
                    var filename = file.GetBTAttributeCIS("file") ?? throw new InvalidDataException("ContentPackage has a child element with no file attribute");
                    var part = fileGetters(filename);

                    fuser.Apply(ret.Root!, part.Root!, name.Fancify(), filename);
                }

                if (ret.Root!.Elements().All(e => e.Name == name && !e.HasAttributes))
                {
                    var newRoot = new XElement(name);
                    foreach (var child in ret.Root.Elements())
                        foreach (var subChild in child.Elements())
                            newRoot.Add(subChild);
                    ret = new XDocument(newRoot);
                }

                return ($"{name.Fancify()}.xml", ret);
            });

            task.Start();
            yield return task;
        }
    }
}
