using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IModApplier
{
    (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files, IEnumerable<string> copies) Apply(XDocument basePackage, Func<string, Task<XDocument>> baseFiles, XDocument modDiff, Func<string, Task<XDocument>> modFiles);
}
