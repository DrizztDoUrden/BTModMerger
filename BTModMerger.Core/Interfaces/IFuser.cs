using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IFuser
{
    void Apply(XElement to, XElement part, string dbgPath, string filename);
}
