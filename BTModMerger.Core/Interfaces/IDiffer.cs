using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IDiffer
{
    XDocument Apply(XDocument @base, string basePath, XDocument mod, string modPath, bool alwaysOverride);
}
