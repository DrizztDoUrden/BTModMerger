using System.Xml.Linq;

namespace BTModMerger;

public static class BTMMSchema
{
    public static readonly string NamespaceAlias = "btmm";
    public static readonly XNamespace Namespace = "https://github.com/DrizztDoUrden/BTModMerger";

    public static readonly string AddNamespaceAlias = "add";
    public static readonly XNamespace AddNamespace = $"{Namespace.NamespaceName}/FakeURI/Add";

    public static readonly string RemoveNamespaceAlias = "remove";
    public static readonly XNamespace RemoveNamespace = $"{Namespace.NamespaceName}/FakeURI/Remove";

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

    public static XElement RemoveElement(int amount, XElement child)
    {
        var ret = new XElement(child)
        {
            Name = RemoveNamespace + child.Name.LocalName,
        };
        if (amount != 1) { ret.SetAttributeValue(Attributes.Amount, amount); }
        return ret;
    }

    public static IEnumerable<XElement> RemoveElements(IEnumerable<XElement> targets)
        => targets
            .Select(target => new XElement(target)
            { 
                Name = RemoveNamespace + target.Name.LocalName
            });

    public static IEnumerable<XElement> RemoveElements(params XElement[] targets) => RemoveElements(targets.AsEnumerable());

    public static XElement Into(string path, IEnumerable<XObject> children) => new(Elements.Into,
        new XAttribute(Attributes.Path, path),
        children
    );

    public static IEnumerable<XElement> AddElements(params XElement[] children) => AddElements(children.AsEnumerable());
    public static IEnumerable<XElement> AddElements(IEnumerable<XElement> children) => children;

    public static XElement AddElements(int amount, XElement child)
    {
        var ret = new XElement(child);
        ret.SetAttributeValue(Attributes.Amount, amount);
        return ret;
    }

    public static XAttribute SetAttribute(string name, string? value) => new(AddNamespace + name, value ?? "");
    public static XAttribute RemoveAttribute(string name) => new(RemoveNamespace + name, "");
}
