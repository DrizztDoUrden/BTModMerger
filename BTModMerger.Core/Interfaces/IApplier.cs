using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface IApplier
{
    void Apply(XElement diffElement, XContainer from, XContainer to, string diffPath);
}
