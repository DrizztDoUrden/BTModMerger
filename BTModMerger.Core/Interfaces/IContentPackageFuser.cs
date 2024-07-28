using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IContentPackageFuser
{
    IAsyncEnumerable<(string path, XName kind, XDocument data)> Apply(XDocument contentPackage, Func<string, XDocument> fileGetters, int threads);
}
