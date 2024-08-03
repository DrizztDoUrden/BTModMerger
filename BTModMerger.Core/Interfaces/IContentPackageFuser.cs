using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IContentPackageFuser
{
    IAsyncEnumerable<(string path, XElement record, XDocument data)> Apply(XDocument contentPackage, Func<string, XDocument> fileGetters, int threads);
}
