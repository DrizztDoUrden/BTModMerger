namespace BTModMerger.Tests.Tools;

using System.Xml.Linq;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;
using Microsoft.Extensions.Logging;

using static BTModMerger.Core.Schema.BTMMSchema;

public class Differ_Tests
{
    private static Differ Make() => new(
            BTMetadata.Test
    );

    [Fact()]
    public void EmptyBase()
    {
        var differ = Make();

        var baseDoc = new XDocument();
        var modDoc = new XDocument(new XElement("RootContainer"));

        Assert.Throws<InvalidDataException>(() => differ.Apply(baseDoc, "base", modDoc, "mod", false));
    }

    [Fact()]
    public void EmptyMod()
    {
        var differ = Make();

        var baseDoc = new XDocument(new XElement("RootContainer"));
        var modDoc = new XDocument();

        Assert.Throws<InvalidDataException>(() => differ.Apply(baseDoc, "base", modDoc, "mod", false));
    }

    [Fact()]
    public void JustRoots()
    {
        var differ = Make();

        var baseDoc = new XDocument(new XElement("RootContainer"));
        var modDoc = new XDocument(new XElement("RootContainer"));

        var expected = new XDocument(Diff());

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", false);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void Attributes()
    {
        var differ = Make();

        var baseDoc = new XDocument(new XElement("RootContainer",
            new XElement("e0", new XAttribute("identifier", "id0"), new XAttribute("old", 123), new XAttribute("same", 234))
        ));

        var modDoc = new XDocument(new XElement("RootContainer",
            new XElement("e0", new XAttribute("identifier", "id0"), new XAttribute("new", 345), new XAttribute("same", 678))
        ));

        var expected = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes,
                PathAttribute("RootContainer/e0[@id0]"),
                new XAttribute(AddNamespace + "new", 345),
                new XAttribute(AddNamespace + "same", 678),
                new XAttribute(RemoveNamespace + "old", "")
            )
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeRemoves()
    {
        var differ = Make();

        var baseDoc = new XDocument(
            new XElement("RootContainer",
                new XElement("e0", new XAttribute("identifier", "id0")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e1", new XAttribute("identifier", "id0"))
            )
        );

        var modDoc = new XDocument(new XElement("RootContainer"));

        var expected = new XDocument(Diff(
            RemoveElement("RootContainer/e0[@id0]"),
            RemoveElement("RootContainer/e0[@id1][0]"),
            RemoveElement("RootContainer/e0[@id1][1]"),
            RemoveElement("RootContainer/e1[@id0]")
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", false);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeRemovesButItIsOverride()
    {
        var differ = Make();

        var baseDoc = new XDocument(
            new XElement("RootContainer",
                new XElement("e0", new XAttribute("identifier", "id0")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e0", new XAttribute("identifier", "id1")),
                new XElement("e1", new XAttribute("identifier", "id0"))
            )
        );

        var modDoc = new XDocument(new XElement("RootContainer"));

        var expected = new XDocument(Diff());

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeRemovesButItIsFusedBase()
    {
        var differ = Make();

        var baseDoc = new XDocument(
            new XElement(Elements.FusedBase,
                new XElement("RootContainer",
                    new XElement("e0", new XAttribute("identifier", "id0")),
                    new XElement("e0", new XAttribute("identifier", "id1")),
                    new XElement("e0", new XAttribute("identifier", "id1")),
                    new XElement("e1", new XAttribute("identifier", "id0"))
                )
            )
        );

        var modDoc = new XDocument(new XElement("RootContainer"));

        var expected = new XDocument(Diff());

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", false);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SomeAdds()
    {
        var differ = Make();

        var baseDoc = new XDocument(new XElement("RootContainer",
            new XElement("e0", new XAttribute("identifier", "id0")),
            new XElement("e0", new XAttribute("identifier", "id1")),
            new XElement("e0", new XAttribute("identifier", "id1")),
            new XElement("e1", new XAttribute("identifier", "id0"))
        ));

        var modDoc = new XDocument(new XElement("RootContainer",
            new XElement("e1", new XAttribute("identifier", "id1")),
            new XElement("e2", new XAttribute("identifier", "id0"))
        ));

        var expected = new XDocument(Diff(
            new XElement("e1", PathAttribute("RootContainer"), new XAttribute("identifier", "id1")),
            new XElement("e2", PathAttribute("RootContainer"), new XAttribute("identifier", "id0"))
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void SimpleOverride()
    {
        var differ = Make();

        var baseDoc = new XDocument(new XElement("RootContainer",
            new XElement("e0", new XAttribute("identifier", "id0")),
            new XElement("e0", new XAttribute("identifier", "id1")),
            new XElement("e1", new XAttribute("identifier", "id0"))
        ));

        var modDoc = new XDocument(new XElement("RootContainer",
            new XElement("override",
                new XElement("e0",
                    new XAttribute("identifier", "id1"),
                    new XAttribute("newAttr", "654"),
                    new XElement("NewChild")
                )
            )
        ));

        var expected = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes, PathAttribute("RootContainer/e0[@id1]"), new XAttribute(AddNamespace + "newAttr", "654")),
            new XElement("NewChild", PathAttribute("RootContainer/e0[@id1]"))
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void ComplexOverride()
    {
        var differ = Make();

        var baseDoc = new XDocument(new XElement("RootContainer",
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

        var modDoc = new XDocument(new XElement("RootContainer",
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
            new XElement(Elements.UpdateAttributes, PathAttribute("RootContainer/e0[@id0]"),
                new XAttribute(AddNamespace + "newAttr", "654"),
                new XAttribute(AddNamespace + "oldAttr", "123"),
                new XAttribute(RemoveNamespace + "removed", "")
            ),
            new XElement("NewChild", PathAttribute("RootContainer/e0[@id0]")),
            new XElement(RemoveNamespace + "Tricky", PathAttribute("RootContainer/e0[@id0]/Container"), AmountAttribute(2), new XAttribute("a0", 5)),
            new XElement("Tricky", PathAttribute("RootContainer/e0[@id0]/Container"), new XAttribute("a0", 1)),
            new XElement(RemoveNamespace + "Tricky", PathAttribute("RootContainer/e0[@id0]/Container"), new XAttribute("a1", 2)),
            RemoveElement("RootContainer/e0[@id0]/RemovedChild")
        ));

        var result = differ.Apply(baseDoc, "base", modDoc, "mod", true);

        Assert.Equal(diff, result, XNode.DeepEquals);
    }
}