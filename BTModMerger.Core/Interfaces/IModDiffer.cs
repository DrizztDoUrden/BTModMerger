using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IModDiffer
{
    (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files) Apply(
        XDocument basePackage,
        Func<string, Task<XDocument>> baseFiles,
        XDocument modFilelist,
        Func<string, Task<XDocument>> modFiles,
        IEnumerable<string> allModXmlFiles,
        bool alwaysOverride);
}
