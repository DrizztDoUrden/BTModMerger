using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IContentPackageFuser
{
    (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files) Apply(XDocument contentPackage, Func<string, Task<XDocument>> fileGetters);
}
