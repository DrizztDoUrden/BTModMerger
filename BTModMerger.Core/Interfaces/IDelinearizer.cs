using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IDelinearizer
{
    XDocument Apply(XDocument input, string inputPath);
}
