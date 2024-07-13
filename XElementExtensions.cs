using System.Xml.Linq;

namespace BTModMerger;

internal static class XElementExtensions
{
    public static string? GetBTIdentifier(this XElement element) => element.Attribute("identifier")?.Value;
    public static int GetBTMMAmount(this XElement element) => int.Parse(element.Attribute(BTMMSchema.Attributes.Amount)?.Value ?? "1");
    public static string? GetBTMMPath(this XElement element) => element.Attribute(BTMMSchema.Attributes.Path)?.Value;
    public static string? GetBTMMValue(this XElement element) => element.Attribute(BTMMSchema.Attributes.Value)?.Value;
}
