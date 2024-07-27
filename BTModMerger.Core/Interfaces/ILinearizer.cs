using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface ILinearizer
{
    XDocument Apply(XDocument input, string inputPath);
}
