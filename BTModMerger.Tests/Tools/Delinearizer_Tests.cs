using System.Xml.Linq;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;

using Microsoft.Extensions.Logging;

namespace BTModMerger.Tests.Tools;

using static BTMMSchema;

public class Delinearizer_Tests
{
    [Fact]
    public void EmptyRoot()
    {
        using var lf = new LoggerFactory();
        var delinearizer = new Delinearizer();

        var test = new XDocument(Diff());

        var result = delinearizer.Apply(test, "");

        Assert.Equal(test, result, XNode.DeepEquals);
    }

    [Fact]
    public void Plain()
    {
        using var lf = new LoggerFactory();
        var delinearizer = new Delinearizer();

        var test = new XDocument(Diff(
            new XElement("e0"),
            new XElement("e1"),
            new XElement("e2")
        ));

        var result = delinearizer.Apply(test, "");

        Assert.Equal(test, result, XNode.DeepEquals);
    }

    [Fact]
    public void MissingUAPath()
    {
        using var lf = new LoggerFactory();
        var delinearizer = new Delinearizer();

        var test = new XDocument(Diff(
            new XElement(Elements.UpdateAttributes)
        ));

        Assert.Throws<InvalidDataException>(() => delinearizer.Apply(test, ""));
    }

    [Fact]
    public void ComplexierCase()
    {
        using var lf = new LoggerFactory();
        var delinearizer = new Delinearizer();

        var nested = new XDocument(Diff(
            Into("p0",
                Into("p1",
                    Into("p2",
                        new XAttribute(AddNamespace + "attr", 123),
                        Into("p3",
                            new XAttribute(AddNamespace + "attr", 123),
                            Into("p4",
                                new XElement("e0")
                            )
                        ),
                        new XElement("e1"),
                        RemoveElement("e3")
                    )
                )
            ),
            new XElement("e2")
        ));

        var linear = new XDocument(Diff(
            Into("p0",
                new XElement(Elements.UpdateAttributes, PathAttribute("p1/p2/p3"), new XAttribute(AddNamespace + "attr", 123)),
                new XElement("e0", PathAttribute("p1/p2/p3/p4")),
                new XElement("e1", PathAttribute("p1/p2")),
                RemoveElement("p1/p2/e3"),
                new XElement(Elements.UpdateAttributes, PathAttribute("p1/p2"), new XAttribute(AddNamespace + "attr", 123))
            ),
            new XElement("e2")
        ));

        var result = delinearizer.Apply(linear, "");

        Assert.Equal(nested, result, XNode.DeepEquals);
    }
}
