namespace BTModMerger.Tests.Tools;

using System.Xml.Linq;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;
using Microsoft.Extensions.Logging;

using static BTModMerger.Core.Schema.BTMMSchema;

public class Differ_Tests
{
    private static Differ Make(LoggerFactory lf) => new(
            new Logger<Differ>(lf),
            BTMetadata.Test
    );

    [Fact()]
    public void EmptyBase()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument();
        var modDoc = new XDocument(new XElement("root"));

        Assert.Throws<InvalidOperationException>(() => differ.Apply(baseDoc, "base", modDoc, "mod", false));
    }

    [Fact()]
    public void EmptyMod()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(new XElement("root"));
        var modDoc = new XDocument();

        Assert.Throws<InvalidOperationException>(() => differ.Apply(baseDoc, "base", modDoc, "mod", false));
    }

    [Fact()]
    public void JustRoots()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(new XElement("root"));
        var modDoc = new XDocument(new XElement("root"));

        var expected = new XDocument(Diff());

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", false);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeRemoves()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(
            new XElement("root",
                new XElement("e0", new XAttribute("identifier", "id0")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e1", new XAttribute("identifier", "id0"))
            )
        );

        var modDoc = new XDocument(new XElement("root"));

        var expected = new XDocument(Diff(
            RemoveElement("root/e0[@id0]"),
            RemoveElement("root/e0[@id1][0]"),
            RemoveElement("root/e0[@id1][1]"),
            RemoveElement("root/e1[@id0]")
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", false);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeRemovesButItIsOverride()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(
            new XElement("root",
                new XElement("e0", new XAttribute("identifier", "id0")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e1", new XAttribute("identifier", "id0"))
            )
        );

        var modDoc = new XDocument(new XElement("root"));

        var expected = new XDocument(Diff());

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeRemovesButItIsFusedBase()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(
            new XElement(Elements.FusedBase,
                new XElement("root",
                    new XElement("e0", new XAttribute("identifier", "id0")),
                    new XElement("e0", new XAttribute("identifier", "id1")),
                    new XElement("e0", new XAttribute("identifier", "id1")),
                    new XElement("e1", new XAttribute("identifier", "id0"))
                )
            )
        );

        var modDoc = new XDocument(new XElement("root"));

        var expected = new XDocument(Diff());

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", false);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeAdds()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(new XElement("root",
            new XElement("e0", new XAttribute("identifier", "id0")),
            new XElement("e0", new XAttribute("identifier", "id1")),
            new XElement("e0", new XAttribute("identifier", "id1")),
            new XElement("e1", new XAttribute("identifier", "id0"))
        ));

        var modDoc = new XDocument(new XElement("root",
            new XElement("e1", new XAttribute("identifier", "id1")),
            new XElement("e2", new XAttribute("identifier", "id0"))
        ));

        var expected = new XDocument(Diff(
            new XElement("e1", PathAttribute("root"), new XAttribute("identifier", "id1")),
            new XElement("e2", PathAttribute("root"), new XAttribute("identifier", "id0"))
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SimpleOverride()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(new XElement("root",
            new XElement("e0", new XAttribute("identifier", "id0")),
            new XElement("e0", new XAttribute("identifier", "id1")),
            new XElement("e1", new XAttribute("identifier", "id0"))
        ));

        var modDoc = new XDocument(new XElement("root",
            new XElement("override",
                new XElement("e0",
                    new XAttribute("identifier", "id1"),
                    new XAttribute("newAttr", "654"),
                    new XElement("NewChild")
                )
            )
        ));

        var expected = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes, PathAttribute("root/e0[@id1]"), new XAttribute(AddNamespace + "newAttr", "654")),
            new XElement("NewChild", PathAttribute("root/e0[@id1]"))
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void ComplexOverride()
    {
        using var lf = new LoggerFactory();
        var differ = Make(lf);

        var baseDoc = new XDocument(new XElement("root",
            new XElement("e0",
                new XAttribute("identifier", "id0"),
                new XAttribute("oldAttr", "875"),
                new XAttribute("removed", "875"),
                new XElement("SameChild"),
                new XElement("OldChild"),
                new XElement("RemovedChild"),
                    new XElement("Container",
                        new XElement("Tricky"),
                        new XElement("Tricky", new XAttribute("a0", 5)),
                        new XElement("Tricky", new XAttribute("a0", 5)),
                        new XElement("Tricky", new XAttribute("a0", 5)),
                        new XElement("Tricky", new XAttribute("a1", 2))
                    )
            )
        ));

        var modDoc = new XDocument(new XElement("root",
            new XElement("override",
                new XElement("e0",
                    new XAttribute("identifier", "id0"),
                    new XAttribute("newAttr", "654"),
                    new XAttribute("oldAttr", "123"),
                    new XElement("SameChild"),
                    new XElement("NewChild"),
                    new XElement("OldChild"),
                    new XElement("Container",
                        new XElement("Tricky"),
                        new XElement("Tricky", new XAttribute("a0", 5)),
                        new XElement("Tricky", new XAttribute("a0", 1))
                    )
                )
            )
        ));

        var diff = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes, PathAttribute("root/e0[@id0]"),
                new XAttribute(AddNamespace + "newAttr", "654"),
                new XAttribute(AddNamespace + "oldAttr", "123"),
                new XAttribute(RemoveNamespace + "removed", "")
            ),
            new XElement("NewChild", PathAttribute("root/e0[@id0]")),
            new XElement("Tricky", PathAttribute("root/e0[@id0]/Container"), new XAttribute("a0", 1)),
            new XElement(RemoveNamespace + "Tricky", PathAttribute("root/e0[@id0]/Container"), AmountAttribute(2), new XAttribute("a0", 5)),
            new XElement(RemoveNamespace + "Tricky", PathAttribute("root/e0[@id0]/Container"), new XAttribute("a1", 2)),
            RemoveElement("root/e0[@id0]/RemovedChild")
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(diff, result, XNode.DeepEquals);
    }
}