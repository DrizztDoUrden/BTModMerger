using System.Xml.Linq;

namespace BTModMerger.Core.Interfaces;

public interface ISimplifier
{
    public readonly record struct Options(AddNamespacePolicy AddNamespacePolicy, ConflictHandlingPolicy conflictHandlingPolicy);

    XDocument Apply(XDocument input, string inputPath, in Options options, XElement? conflictsRoot);
}
