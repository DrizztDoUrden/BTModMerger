using System.Xml.Linq;

namespace BTModMerger;

public static class BTMMSchema
{
    public static readonly string NamespaceAlias = "btmm";
    public static readonly XNamespace Namespace = "https://github.com/DrizztDoUrden/BTModMerger";

    public static class Elements
    {
        public static readonly XName Diff = Namespace + nameof(Diff);
        public static readonly XName Into = Namespace + nameof(Into);
        public static readonly XName AddElements = Namespace + nameof(AddElements);
        public static readonly XName RemoveElements = Namespace + nameof(RemoveElements);
        public static readonly XName SetAttribute = Namespace + nameof(SetAttribute);
        public static readonly XName RemoveAttribute = Namespace + nameof(RemoveAttribute);
    }

    public static class Attributes
    {
        public static readonly XName Path = Namespace + nameof(Path);
        public static readonly XName Value = Namespace + nameof(Value);
        public static readonly XName Amount = Namespace + nameof(Amount);
    }

    public static XElement RemoveElement(string path) => new(Elements.RemoveElements,
        new XAttribute(Attributes.Path, path)
    );

    public static XElement RemoveElements(IEnumerable<XElement> targets) => new(Elements.RemoveElements, targets);
    public static XElement RemoveElement(params XElement[] targets) => RemoveElements(targets.AsEnumerable());

    public static XElement Into(string path, IEnumerable<XElement> children) => new(Elements.Into,
        new XAttribute(Attributes.Path, path),
        children
    );

    public static XElement AddElements(params XElement[] children) => AddElements(children.AsEnumerable());
    public static XElement AddElements(IEnumerable<XElement> children) => new(Elements.AddElements, children);

    public static XElement AddElements(int amount, params XElement[] children) => AddElements(amount, children.AsEnumerable());
    public static XElement AddElements(int amount, IEnumerable<XElement> children) => new(Elements.AddElements,
        new XAttribute(Attributes.Amount, amount),
        children
    );
}
