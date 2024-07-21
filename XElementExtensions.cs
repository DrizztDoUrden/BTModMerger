using System.Xml.Linq;

namespace BTModMerger;

internal static class XElementExtensions
{
    public static bool IsBTOverride(this XElement element) => element.Name.Namespace == XNamespace.None && CompareCIS(element.Name.LocalName, "override");

    public static int GetBTMMAmount(this XElement element) => int.Parse(element.Attribute(BTMMSchema.Attributes.Amount)?.Value ?? "1");
    public static string? GetBTMMPath(this XElement element) => element.Attribute(BTMMSchema.Attributes.Path)?.Value;
    public static string? GetBTMMValue(this XElement element) => element.Attribute(BTMMSchema.Attributes.Value)?.Value;

    public static string? GetBTIdentifier(this XElement element) => BTMetadata.Instance.GetId(element);

    public static string? GetBTAttributeCIS(this XElement element, XName name)
        => element.Attributes().FirstOrDefault(attr => attr.Name.Namespace == name.Namespace && CompareCIS(attr.Name.LocalName, name.LocalName))?.Value;

    public static XAttribute? FindBTAttributeCIS(this XElement element, XName name)
        => element.Attributes().FirstOrDefault(attr => attr.Name.Namespace == name.Namespace && CompareCIS(attr.Name.LocalName, name.LocalName));

    private static bool CompareCIS(string l, string r)
        => l.Equals(r, StringComparison.CurrentCultureIgnoreCase);

    public static bool IsTricky(this XElement element)
        => BTMetadata.Instance.Tricky.Contains(element.Name.LocalName.ToLower());

    public static bool IsIndexed(this XElement element)
        => BTMetadata.Instance.Indexed.Contains(element.Name.LocalName.ToLower())
            || element.IsTricky();

    public static void SetAttributeCIS(this XElement target, string name, object value)
    {
        var attr = target.FindBTAttributeCIS(name);

        if (attr is not null)
        {
            attr.Value = value.ToString()!;
            return;
        }

        target.SetAttributeValue(name, value);
    }

    public static void SetAttributeSorting(this XElement target, XName name, object value)
    {
        var attrs = target.Attributes()
            .Where(attr => attr.Name != name)
            .ToArray();

        var btmmAtrrs = attrs.Where(attr => attr.Name.Namespace != XNamespace.None).ToArray();
        var btAtrrs = attrs.Where(attr => attr.Name.Namespace == XNamespace.None).ToArray();

        target.RemoveAttributes();
        target.SetAttributeValue(name, value.ToString());
        foreach (var attr in btmmAtrrs)
            target.SetAttributeValue(attr.Name, attr.Value);
        foreach (var attr in btAtrrs)
            target.SetAttributeValue(attr.Name, attr.Value);
    }
}
