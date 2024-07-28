using System.Xml.Linq;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;

using Microsoft.Extensions.Logging;

namespace BTModMerger.Tests.Tools;

using static BTMMSchema;

public class Linearizer_Tests
{
    [Fact]
    public void EmptyRoot()
    {
        using var lf = new LoggerFactory();
        var linearizer = new Linearizer();

        var test = new XDocument(Diff());

        var result = linearizer.Apply(test, "");

        Assert.Equal(test, result, XNode.DeepEquals);
    }

    [Fact]
    public void AlreadyLinear()
    {
        using var lf = new LoggerFactory();
        var linearizer = new Linearizer();

        var test = new XDocument(Diff(
            new XElement("e0"),
            new XElement("e1"),
            new XElement("e2")
        ));

        var result = linearizer.Apply(test, "");

        Assert.Equal(test, result, XNode.DeepEquals);
    }

    [Fact]
    public void MissingIntoPath()
    {
        using var lf = new LoggerFactory();
        var linearizer = new Linearizer();

        var test = new XDocument(Diff(
            new XElement(Elements.Into)
        ));

        Assert.Throws<InvalidDataException>(() => linearizer.Apply(test, ""));
    }

    [Fact]
    public void ComplexierCase()
    {
        using var lf = new LoggerFactory();
        var linearizer = new Linearizer();

        var test = new XDocument(Diff(
            Into("p0/p1/p2",
                new XAttribute(AddNamespace + "attr", 123),
                new XElement(Elements.UpdateAttributes,
                    PathAttribute("p3")
                ),
                Into("p3",
                    new XAttribute(AddNamespace + "attr", 123),
                    new XElement("e0", PathAttribute("p4"))
                ),
                new XElement("e1")
            ),
            new XElement("e2")
        ));

        var linear = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes, PathAttribute("p0/p1/p2/p3"), new XAttribute(AddNamespace + "attr", 123)),
            new XElement("e0", PathAttribute("p0/p1/p2/p3/p4")),
            new XElement("e1", PathAttribute("p0/p1/p2")),
            new XElement(Elements.UpdateAttributes, PathAttribute("p0/p1/p2"), new XAttribute(AddNamespace + "attr", 123)),
            new XElement("e2")
        ));

        var result = linearizer.Apply(test, "");

        Assert.Equal(linear, result, XNode.DeepEquals);
    }
}
