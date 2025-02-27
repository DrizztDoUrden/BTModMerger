﻿using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace BTModMerger.Core.Schema;

using static BTMMSchema;

public static class XElementExtensions
{
    public static bool IsBTOverride(this XElement element, BTMetadata metadata)
        => element.Name.Namespace == XNamespace.None && metadata.OverrideNodes.Contains(element.Name.LocalName.ToLower());

    public static bool IsRootContainer(this XElement element, BTMetadata metadata)
        => element.Name.Namespace == XNamespace.None && metadata.RootContainers.Contains(element.Name.LocalName.ToLower()) ||
            element.Name == Elements.FusedBase;

    public static int GetBTMMAmount(this XElement element)
        => int.Parse(element.Attribute(Attributes.Amount)?.Value ?? "1");

    public static string? GetBTMMPath(this XElement element)
        => element.Attribute(Attributes.Path)?.Value;

    public static string? GetBTIdentifier(this XElement element, BTMetadata metadata)
        => metadata.GetId(element);

    public static bool IsEqualCIS(this XName left, XName right)
        => left.Namespace == right.Namespace && CompareCIS(left.LocalName, right.LocalName);

    public static bool IsNameEqualCIS(this XElement element, XName name)
        => element.Name.IsEqualCIS(name);

    public static bool IsNameEqualCIS(this XAttribute attribute, XName name)
        => attribute.Name.IsEqualCIS(name);

    public static string? GetBTAttributeCIS(this XElement element, XName name)
        => element.FindBTAttributeCIS(name)?.Value;

    public static XAttribute? FindBTAttributeCIS(this XElement element, XName name)
        => element.Attributes().FirstOrDefault(attr => attr.IsNameEqualCIS(name));

    private static bool CompareCIS(string l, string r)
        => l.Equals(r, StringComparison.CurrentCultureIgnoreCase);

    public static IEnumerable<XElement> ElementsCIS(this XContainer container, XName name)
        => container.Elements().Where(e => e.IsNameEqualCIS(name));

    public static bool IsTricky(this XElement element, BTMetadata metadata)
        => metadata.Tricky.Contains(element.Name.LocalName.ToLower());

    public static bool IsIndexed(this XElement element, BTMetadata metadata)
        => metadata.Indexed.Contains(element.Name.LocalName.ToLower())
            || element.IsTricky(metadata);

    public static void SetAttributeCIS(this XElement target, XName name, object value)
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
        target.SetAttributeValue(name, value.ToString());
        target.SortAttributes();
    }

    public static void SortAttributes(this XElement target)
    {
        var attrs = target.Attributes().ToArray();

        var btmmAtrrs = attrs.Where(attr => attr.Name.Namespace != XNamespace.None).OrderBy(attr => attr.Name.NamespaceName).ToArray();
        var btAtrrs = attrs.Where(attr => attr.Name.Namespace == XNamespace.None).ToArray();

        target.RemoveAttributes();
        foreach (var attr in btmmAtrrs)
            target.SetAttributeValue(attr.Name, attr.Value);
        foreach (var attr in btAtrrs)
            target.SetAttributeValue(attr.Name, attr.Value);
    }

    public static bool RootIs(this XDocument document, XName name)
        => document.Root is not null &&
            document.Root.Name == name;

    public static bool RootIsCIS(this XDocument document, XName name)
        => document.Root is not null &&
            document.Root.IsNameEqualCIS(name);
}
