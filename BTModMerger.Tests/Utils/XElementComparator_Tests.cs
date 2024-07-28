using System.Xml.Linq;
using BTModMerger.Core.Schema;

namespace BTModMerger.Tests;

public class XElementComparator_Tests
{
    [Fact()]
    public void Deduplicate()
    {
        Assert.Collection(
            XElementComparator.Deduplicate(
                new XElement[]
                {
                    new("e"),
                    new("e"),
                    new("e2"),
                }
            ),
            e =>
            {
                Assert.Equal(2, e.count);
                Assert.Equal(new("e"), e.item, XNode.DeepEquals);
            },
            e =>
            {
                Assert.Equal(1, e.count);
                Assert.Equal(new("e2"), e.item, XNode.DeepEquals);
            }
        );
    }

    [Fact()]
    public void Deduplicate_Counts()
    {
        Assert.Collection(
            XElementComparator.Deduplicate(
                new (XElement, int)[]
                {
                    (new("e"), 1),
                    (new("e"), 2),
                    (new("e2"), 5),
                }
            ),
            e =>
            {
                Assert.Equal(3, e.count);
                Assert.Equal(new("e"), e.item, XNode.DeepEquals);
            },
            e =>
            {
                Assert.Equal(5, e.count);
                Assert.Equal(new("e2"), e.item, XNode.DeepEquals);
            }
        );
    }

    [Fact()]
    public void Deduplicate_Containers()
    {
        var c0 = new XElement("c0");
        var c1 = new XElement("c1");
        var r0 = new XElement("r0");
        var r1 = new XElement("r1");

        Assert.Collection(
            XElementComparator.Deduplicate(
                new (XElement item, XContainer container, int count, XElement request)[]
                {
                    (new("e"), c0, 1, r0),
                    (new("e"), c0, 2, r0),
                    (new("e2"), c1, 1, r0),
                    (new("e3"), c1, 5, r0),
                    (new("e4"), c1, 4, r1),
                }
            ),
            e =>
            {
                Assert.Equal(3, e.count);
                Assert.Equal(new("e"), e.item, XNode.DeepEquals);
                Assert.Equal(c0, (XElement)e.container);
                Assert.Equal(r0, e.request);
            },
            e =>
            {
                Assert.Equal(1, e.count);
                Assert.Equal(new("e2"), e.item, XNode.DeepEquals);
                Assert.Equal(c1, (XElement)e.container);
                Assert.Equal(r0, e.request);
            },
            e =>
            {
                Assert.Equal(5, e.count);
                Assert.Equal(new("e3"), e.item, XNode.DeepEquals);
                Assert.Equal(c1, (XElement)e.container);
                Assert.Equal(r0, e.request);
            },
            e =>
            {
                Assert.Equal(4, e.count);
                Assert.Equal(new("e4"), e.item, XNode.DeepEquals);
                Assert.Equal(c1, (XElement)e.container);
                Assert.Equal(r1, e.request);
            }
        );
    }

    [Fact()]
    public void NormalizeAttributes_Empty()
    {
        Assert.Empty(XElementComparator.NormalizeAttributes(new XElement("e")));
    }

    [Fact()]
    public void NormalizeAttributes()
    {
        var ns = XNamespace.Get("https://ns");

        Assert.Collection(
            XElementComparator.NormalizeAttributes(
                new XElement("e",
                    new XAttribute("attr2", 123),
                    new XAttribute("attr3", 231),
                    new XAttribute(ns + "attr1", 312)
                )),
            attr => {
                Assert.Equal("attr2", attr.Name);
                Assert.Equal("123", attr.Value);
            },
            attr => {
                Assert.Equal("attr3", attr.Name);
                Assert.Equal("231", attr.Value);
            },
            attr => {
                Assert.Equal(ns + "attr1", attr.Name);
                Assert.Equal("312", attr.Value);
            }
        );
    }

    [Fact()]
    public void NormalizeElement()
    {
        var initial = new XElement("e",
            new XAttribute(XNamespace.Xmlns + "ns", "https://Test"),
            new XElement("child_xyz"),
            new XComment("comment"),
            new XElement("child_abc", new XAttribute("z", 1), new XAttribute("a", 2)),
            new XText("text")
        );

        var ordered = new XElement("e",
            new XElement("child_xyz"),
            new XElement("child_abc", new XAttribute("a", 2), new XAttribute("z", 1)),
            new XText("text")
        );

        Assert.Equal(ordered, XElementComparator.NormalizeElement(initial), XNode.DeepEquals);
    }
}