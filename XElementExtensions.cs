﻿using System.Xml.Linq;

namespace BTModMerger;

internal static class XElementExtensions
{
    public static bool IsBTOverride(this XElement element) => element.Name.Namespace == XNamespace.None && CompareCIS(element.Name.LocalName, "override");

    public static int GetBTMMAmount(this XElement element) => int.Parse(element.Attribute(BTMMSchema.Attributes.Amount)?.Value ?? "1");
    public static string? GetBTMMPath(this XElement element) => element.Attribute(BTMMSchema.Attributes.Path)?.Value;
    public static string? GetBTMMValue(this XElement element) => element.Attribute(BTMMSchema.Attributes.Value)?.Value;

    public static string? GetBTIdentifier(this XElement element)
    {
        if (element.Name.Namespace == XNamespace.None)
        {
            if (CompareCIS(element.Name.LocalName, "character"))
            {
                return element.GetBTAttributeCIS("group") ?? "btmm::-"
                    + ":"
                    + element.GetBTAttributeCIS("speciesname") ?? "btmm::-";

            }
        }

        return element.Attribute("identifier")?.Value;
    }

    public static string? GetBTAttributeCIS(this XElement element, string name)
        => element.Attributes().FirstOrDefault(attr => CompareCIS(attr.Name.LocalName, "group"))?.Value;

    private static bool CompareCIS(string l, string r)
        => l.Equals(r, StringComparison.CurrentCultureIgnoreCase);

    public static bool IsIndexed(this XElement element)
        => BTMetadata.Instance.Indexed.Contains(element.Name.LocalName.ToLower())
            || BTMetadata.Instance.Tricky.Contains(element.Name.LocalName.ToLower());
}
