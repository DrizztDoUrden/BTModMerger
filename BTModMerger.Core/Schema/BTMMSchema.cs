using System.Xml.Linq;

namespace BTModMerger.Core.Schema;

public static class BTMMSchema
{
    public static readonly string NamespaceAlias = "btmm";
    public static readonly XNamespace Namespace = "https://github.com/DrizztDoUrden/BTModMerger";

    public static readonly string AddNamespaceAlias = "add";
    public static readonly XNamespace AddNamespace = $"{Namespace.NamespaceName}/FakeURI/Add";

    public static readonly string RemoveNamespaceAlias = "remove";
    public static readonly XNamespace RemoveNamespace = $"{Namespace.NamespaceName}/FakeURI/Remove";

    public static readonly Dictionary<XNamespace, string> NamespaceAliases = new()
    {
        [Namespace] = NamespaceAlias,
        [AddNamespace] = AddNamespaceAlias,
        [RemoveNamespace] = RemoveNamespaceAlias,
    };

    public static class Elements
    {
        public static readonly XName Override = nameof(Override);

        public static readonly XName Diff = Namespace + nameof(Diff);
        public static readonly XName Into = Namespace + nameof(Into);
        public static readonly XName UpdateAttributes = Namespace + nameof(UpdateAttributes);
        public static readonly XName RemoveElement = Namespace + nameof(RemoveElement);

        public static readonly XName FusedBase = Namespace + nameof(FusedBase);

        public static readonly XName ContentPackage = Namespace + nameof(ContentPackage);
        public static readonly XName Part = Namespace + nameof(Part);
    }

    public static class Attributes
    {
        public static readonly XName Path = Namespace + nameof(Path);
        public static readonly XName Amount = Namespace + nameof(Amount);
        public static readonly XName File = Namespace + nameof(File);
    }

    public static XElement RemoveElement(string path) => new(Elements.RemoveElement,
        new XAttribute(Attributes.Path, path)
    );

    public static XElement RemoveElement(string path, int amount, XElement child)
    {
        var ret = new XElement(child)
        {
            Name = RemoveNamespace + child.Name.LocalName,
        };
        if (!string.IsNullOrEmpty(path)) ret.SetAttributeSorting(Attributes.Path, path);
        if (amount != 1) ret.SetAttributeSorting(Attributes.Amount, amount); return ret;
    }

    public static XElement Into(string path, params XObject[] children) => Into(path, children.AsEnumerable());
    public static XElement Into(string path, IEnumerable<XObject> children) => new(Elements.Into,
        new XAttribute(Attributes.Path, path),
        children
    );

    public static IEnumerable<XElement> AddElements(string path, IEnumerable<XElement> children)
        => children
            .Select(item =>
            {
                var copy = new XElement(item);
                if (!string.IsNullOrEmpty(path))
                    copy.SetAttributeSorting(Attributes.Path, path);
                return copy;
            });

    public static XElement AddElements(string path, int amount, XElement child)
    {
        var ret = new XElement(child);
        if (!string.IsNullOrEmpty(path)) ret.SetAttributeSorting(Attributes.Path, path);
        if (amount != 1) ret.SetAttributeSorting(Attributes.Amount, amount);
        return ret;
    }

    public static XAttribute SetAttribute(string name, string value) => new(AddNamespace + name, value);

    public static XAttribute AmountAttribute(int value) => new(Attributes.Amount, value);
    public static XAttribute PathAttribute(string value) => new(Attributes.Path, value);
    public static XAttribute FileAttribute(string value) => new(Attributes.File, value);

    public static XElement UpdateAttributes() => new(Elements.UpdateAttributes);

    public static XElement Diff(params object[] children)
        => new(Elements.Diff,
            new XAttribute(XNamespace.Xmlns + NamespaceAlias, Namespace),
            new XAttribute(XNamespace.Xmlns + AddNamespaceAlias, AddNamespace),
            new XAttribute(XNamespace.Xmlns + RemoveNamespaceAlias, RemoveNamespace),
            children
        );

    public static XElement FusedBase(params object[] children)
        => new(Elements.FusedBase,
            new XAttribute(XNamespace.Xmlns + NamespaceAlias, Namespace),
            children
        );

    public static XElement ContentPackage(params object[] children)
        => new(Elements.ContentPackage,
            new XAttribute(XNamespace.Xmlns + NamespaceAlias, Namespace),
            children
        );

    public static XElement Part(string path) => new(Elements.Part, PathAttribute(path));
}
