using System.Xml.Linq;
using BTModMerger.Core.Schema;

namespace BTModMerger.Tests;

using static BTMMSchema;

public class XElementExtensions_Tests
{
    [Fact]
    public void Override()
    {
        Assert.True(new XElement("oVeRRide").IsBTOverride(BTMetadata.Test));
        Assert.False(new XElement("Jobs").IsBTOverride(BTMetadata.Test));
        Assert.False(new XElement(Namespace + "Override").IsBTOverride(BTMetadata.Test));
    }

    [Fact]
    public void Amount_Missing() => Assert.Equal(1, new XElement("e", new XAttribute("attr", 123)).GetBTMMAmount());

    [Fact]
    public void Amount_InvalidFormat() => Assert.Throws<FormatException>(() => new XElement("e", new XAttribute(Attributes.Amount, "sadc")).GetBTMMAmount());

    [Fact]
    public void Amount_InvalidCase() => Assert.Equal(1, new XElement("e", new XAttribute(Namespace + "amount", 123)).GetBTMMAmount());

    [Fact]
    public void Amount() => Assert.Equal(123, new XElement("e", new XAttribute(Attributes.Amount, 123)).GetBTMMAmount());

    [Fact]
    public void Path_Missing() => Assert.Null(new XElement("e", new XAttribute("attr", 123)).GetBTMMPath());

    [Fact]
    public void Path() => Assert.Equal("123/456", new XElement("e", new XAttribute(Attributes.Path, "123/456")).GetBTMMPath());

    [Fact]
    public void Identifier_Missing() => Assert.Null(new XElement("e", new XAttribute("attr", 123)).GetBTIdentifier(BTMetadata.Test));

    [Fact]
    public void Identifier() => Assert.Equal("123", new XElement("e", new XAttribute("idEnTifiEr", "123")).GetBTIdentifier(BTMetadata.Test));

    [Fact]
    public void FindBTAttributeCIS_Missing() => Assert.Null(new XElement("e", new XAttribute("attr", 123)).FindBTAttributeCIS("identifier"));

    [Fact]
    public void FindBTAttributeCIS_WrongNS() => Assert.Null(new XElement("e", new XAttribute(Namespace + "attr", 123)).FindBTAttributeCIS("identifier"));

    [Fact]
    public void FindBTAttributeCIS() => Assert.Equal("123", new XElement("e", new XAttribute("idEnTifiEr", "123")).FindBTAttributeCIS("identifier")?.Value);

    [Fact]
    public void IsIndexed_Miss() => Assert.False(new XElement("e").IsIndexed(BTMetadata.Test));

    [Fact]
    public void IsIndexed_Tricky() => Assert.True(new XElement("Tricky").IsIndexed(BTMetadata.Test));

    [Fact]
    public void IsIndexed() => Assert.True(new XElement("Indexed").IsIndexed(BTMetadata.Test));

    [Fact]
    public void IsTricky() => Assert.False(new XElement("e").IsTricky(BTMetadata.Test));

    [Fact]
    public void GetAttributeCIS_Miss() => Assert.Null(new XElement("e").GetBTAttributeCIS("attr"));

    [Fact]
    public void GetAttributeCIS() => Assert.Equal("123", new XElement("e", new XAttribute("attr", 123)).GetBTAttributeCIS("attr"));

    [Fact]
    public void SetAttributeCIS_New()
    {
        var element = new XElement("e");
        element.SetAttributeCIS("attr", 123);
        Assert.Equal("123", element.GetBTAttributeCIS("attr"));
    }

    [Fact]
    public void SetAttributeCIS_Existing()
    {
        var element = new XElement("e", new XAttribute("attr", 4325));
        element.SetAttributeCIS("attr", 123);
        Assert.Equal("123", element.GetBTAttributeCIS("attr"));
    }

    [Fact]
    public void SetAttributeSorting_New()
    {
        var element = new XElement("e", new XAttribute("attr1", 4325), new XAttribute("attr2", 4325));
        element.SetAttributeSorting("attr", 123);
        Assert.Equal("123", element.GetBTAttributeCIS("attr"));
    }

    [Fact]
    public void SetAttributeSorting_Existing()
    {
        var element = new XElement("e", new XAttribute("attr1", 4325), new XAttribute("attr", 4325), new XAttribute(Namespace + "attr2", 76));
        element.SetAttributeSorting("attr", 123);

        Assert.Collection(element.Attributes(),
            attr =>
            {
                Assert.Equal(Namespace, attr.Name.Namespace);
                Assert.Equal("attr2", attr.Name.LocalName);
                Assert.Equal("76", attr.Value);
            },
            attr =>
            {
                Assert.Equal(XNamespace.None, attr.Name.Namespace);
                Assert.Equal("attr1", attr.Name.LocalName);
                Assert.Equal("4325", attr.Value);
            },
            attr =>
            {
                Assert.Equal(XNamespace.None, attr.Name.Namespace);
                Assert.Equal("attr", attr.Name.LocalName);
                Assert.Equal("123", attr.Value);
            }
        );
    }
}
