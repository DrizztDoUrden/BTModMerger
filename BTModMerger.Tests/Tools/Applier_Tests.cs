using System.Xml.Linq;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;
using Microsoft.Extensions.Logging;

namespace BTModMerger.Tests.Tools;

using static BTMMSchema;

public class Applier_Tests
{
    private Applier Make(LoggerFactory lf) => new(
        new Logger<Applier>(lf),
        BTMetadata.Test
    );

    [Fact]
    public void Empty()
    {
        using var lf = new LoggerFactory();
        var applier = Make(lf);

        var baseDoc = new XDocument(new XElement("root"));
        var diffDoc = new XDocument(Diff());
        var expected = new XDocument();
        var result = new XDocument();

        applier.Apply(diffDoc.Root!, baseDoc, result, "test/Diff");

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact]
    public void SimpleCaseOverride()
    {
        using var lf = new LoggerFactory();
        var applier = Make(lf);

        var baseDoc = new XDocument(new XElement("root",
            new XElement("item0",
                new XAttribute("identifier", "id0"),
                new XElement("prop0",
                    new XAttribute("attr0", "00"),
                    new XAttribute("attr1", "01")
                ),
                new XElement("prop1",
                    new XElement("child0")
                )
            ),
            new XElement("item1",
                new XAttribute("identifier", "id0"),
                new XElement("prop0",
                    new XAttribute("attr0", "10")
                ),
                new XElement("prop1",
                    new XAttribute("attr1", "11")
                )
            )
        ));

        var diffDoc = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes,
                PathAttribute("root/item0[@id0]/prop0"),
                new XAttribute(AddNamespace + "attr2", "02"),
                new XAttribute(RemoveNamespace + "attr1", "")
            ),
            new XElement("child1",
                PathAttribute("root/item0[@id0]/prop1")
            ),
            RemoveElement("root/item0[@id0]/prop1/child0")
        ));

        var expected = new XDocument(
            new XElement("Override",
                new XElement("root",
                    new XElement("item0",
                        new XAttribute("identifier", "id0"),
                        new XElement("prop0",
                            new XAttribute("attr0", "00"),
                            new XAttribute("attr2", "02")
                        ),
                        new XElement("prop1",
                            new XElement("child1")
                        )
                    )
                )
            )
        );

        var result = new XDocument();

        applier.Apply(diffDoc.Root!, baseDoc, result, "test/Diff");

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact]
    public void SimpleCase()
    {
        using var lf = new LoggerFactory();
        var applier = Make(lf);

        var baseDoc = new XDocument(
            new XElement("root",
                new XElement("item0",
                    new XAttribute("identifier", "id0"),
                    new XElement("prop0",
                        new XAttribute("attr0", "00"),
                        new XAttribute("attr1", "01")
                    ),
                    new XElement("prop1",
                        new XElement("child0")
                    )
                ),
                new XElement("item1",
                    new XAttribute("identifier", "id0"),
                    new XElement("prop0",
                        new XAttribute("attr0", "10")
                    ),
                    new XElement("prop1",
                        new XAttribute("attr1", "11")
                    )
                )
            )
        );

        var diffDoc = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes,
                PathAttribute("root/item0[@id0]/prop0"),
                new XAttribute(AddNamespace + "attr2", "02"),
                new XAttribute(RemoveNamespace + "attr1", "")
            ),
            new XElement(AddNamespace + "child1",
                PathAttribute("root/item0[@id0]/prop1")
            ),
            new XElement(RemoveNamespace + "child0",
                PathAttribute("root/item0[@id0]/prop1")
            )
        ));

        var expected = new XDocument(
            new XElement("root",
                new XElement("item0",
                    new XAttribute("identifier", "id0"),
                    new XElement("prop0",
                        new XAttribute("attr0", "00"),
                        new XAttribute("attr2", "02")
                    ),
                    new XElement("prop1",
                        new XElement("child1")
                    )
                ),
                new XElement("item1",
                    new XAttribute("identifier", "id0"),
                    new XElement("prop0",
                        new XAttribute("attr0", "10")
                    ),
                    new XElement("prop1",
                        new XAttribute("attr1", "11")
                    )
                )
            )
        );

        var result = new XDocument(baseDoc);

        applier.Apply(diffDoc.Root!, baseDoc, result, "test/Diff");

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact()]
    public void Complex()
    {
        using var lf = new LoggerFactory();
        var applier = Make(lf);

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

        var modDoc = new XDocument(
            new XElement("Override",
                new XElement("root",
                    new XElement("e0",
                        new XAttribute("identifier", "id0"),
                        new XAttribute("oldAttr", "123"),
                        new XAttribute("newAttr", "654"),
                        new XElement("SameChild"),
                        new XElement("OldChild"),
                        new XElement("Container",
                            new XElement("Tricky"),
                            new XElement("Tricky", new XAttribute("a0", 5)),
                            new XElement("Tricky", new XAttribute("a0", 1))
                        ),
                        new XElement("NewChild")
                    )
                )
            )
        );

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

        var result = new XDocument();

        applier.Apply(diff.Root!, baseDoc, result, "test/Diff");

        Assert.Equal(modDoc, result, XNode.DeepEquals);
    }
}
