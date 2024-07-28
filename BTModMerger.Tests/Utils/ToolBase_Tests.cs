using System.Xml.Linq;
using BTModMerger.Core;

namespace BTModMerger.Tests.Utils;

using static BTMMSchema;

public class ToolBase_Tests
{
    [Fact]
    public void FindSubscript_Missing() => Assert.Equal((-1, -1), ToolBase.FindSubscript("e", ""));

    [Fact]
    public void FindSubscript_Id() => Assert.Equal((1, 5), ToolBase.FindSubscript("e[@id]", ""));

    [Fact]
    public void FindSubscript_Index() => Assert.Equal((1, 4), ToolBase.FindSubscript("e[11]", ""));

    [Fact]
    public void FindSubscript_Both() => Assert.Equal((1, 5), ToolBase.FindSubscript("e[@id][11]", ""));

    [Fact]
    public void FindSubscript_SyntaxError0() => Assert.Throws<InvalidDataException>(() => ToolBase.FindSubscript("e[@id", ""));

    [Fact]
    public void FindSubscript_SyntaxError1() => Assert.Throws<InvalidDataException>(() => ToolBase.FindSubscript("e[@id[", ""));

    [Fact]
    public void FindSubscript_SyntaxError2() => Assert.Throws<InvalidDataException>(() => ToolBase.FindSubscript("e[@id[123][]", ""));

    [Fact]
    public void FindSubscript_SyntaxError3() => Assert.Throws<InvalidDataException>(() => ToolBase.FindSubscript("e[][123]", ""));

    [Fact]
    public void ExtractSubscript_Both()
    {
        var path = "element[@id][123]";
        var sss = ToolBase.ExtractSubscripts(ref path, "");
        Assert.Equal("element", path);
        Assert.Equal("@id", sss.ss0);
        Assert.Equal("123", sss.ss1);
    }

    [Fact]
    public void ExtractSubscript_None()
    {
        var path = "element";
        var sss = ToolBase.ExtractSubscripts(ref path, "");
        Assert.Equal("element", path);
        Assert.Null(sss.ss0);
        Assert.Null(sss.ss1);
    }

    [Fact]
    public static void ParseSubscript_Empty() => Assert.Equal((null, -1), ToolBase.ParseSubscript(null));

    [Fact]
    public static void ParseSubscript_Id() => Assert.Equal(("id", -1), ToolBase.ParseSubscript("@id"));

    [Fact]
    public static void ParseSubscript_Index() => Assert.Equal((null, 123), ToolBase.ParseSubscript("123"));

    [Fact]
    public static void ParseSubscript_Error() => Assert.Throws<FormatException>(() => ToolBase.ParseSubscript("id"));

    [Fact]
    public static void Fancify_Miss() => Assert.Equal("name", XName.Get("name").Fancify());

    [Fact]
    public static void Fancify() => Assert.Equal("btmm:name", (Namespace + "name").Fancify());

    [Fact]
    public static void CombineBTMMPaths_Empty() => Assert.Equal("e1/e2", ToolBase.CombineBTMMPaths("", "e1", "e2"));

    [Fact]
    public static void CombineBTMMPaths() => Assert.Equal("e1/btmm:e2", ToolBase.CombineBTMMPaths("e1", Namespace + "e2"));

    [Fact]
    public static void SplitPath() => Assert.Equal(new[] { "e1", "e2" }, ToolBase.SplitPath(" e1 /  /e2"));

    [Fact]
    public static void FilterBySubscripts_Miss() => Assert.Empty(
        ToolBase.FilterBySubscripts(
            [
                new("e"),
                new("e"),
            ],
            ("@id", "1"),
            "",
            BTMetadata.Test
        )
    );

    [Fact]
    public static void FilterBySubscripts_Multiple() => Assert.Collection(
        ToolBase.FilterBySubscripts(
            [
                new("e", new XAttribute("identifier", "id")),
                new("e", new XAttribute("identifier", "i")),
                new("e", new XAttribute("identifier", "id")),
            ],
            ("@id", null),
            "",
            BTMetadata.Test
        ),
        e => Assert.Equal(e, new XElement("e", new XAttribute("identifier", "id")), XNode.DeepEquals),
        e => Assert.Equal(e, new("e", new XAttribute("identifier", "id")), XNode.DeepEquals)
    );

    [Fact]
    public static void FilterBySubscripts() => Assert.Collection(
        ToolBase.FilterBySubscripts(
            [
                new("e", new XAttribute("identifier", "i")),
                new("e", new XAttribute("identifier", "id")),
                new("e", new XAttribute("identifier", "i")),
                new("e", new XAttribute("identifier", "id"), new XAttribute("hit", "true")),
                new("e", new XAttribute("identifier", "id")),
                new("e", new XAttribute("identifier", "i")),
            ],
            ("@id", "1"),
            "",
            BTMetadata.Test
        ),
        e => Assert.Equal(e, new("e", new XAttribute("identifier", "id"), new XAttribute("hit", "true")), XNode.DeepEquals)
    );
}
