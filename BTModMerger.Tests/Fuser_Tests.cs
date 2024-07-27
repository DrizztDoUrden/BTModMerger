using System.Xml.Linq;

using Microsoft.Extensions.Logging;

namespace BTModMerger.Tests;

using static BTMMSchema;

public class Fuser_Tests
{
    private Fuser Make(LoggerFactory lf) => new(
        new Logger<Fuser>(lf),
        BTMetadata.Test,
        new Linearizer(new Logger<Linearizer>(lf)),
        new Delinearizer(new Logger<Delinearizer>(lf)),
        new Simplifier(
            new Logger<Simplifier>(lf),
            BTMetadata.Test,
            new Delinearizer(new Logger<Delinearizer>(lf))
        )
    );

    [Fact]
    public void Trivial()
    {
        using var lf = new LoggerFactory();
        var fuser = Make(lf);

        var source = new XDocument(Diff(
            new XElement("e0"),
            new XElement("e1")
        ));

        var result = new XDocument(Diff());

        fuser.Apply(result.Root!, source.Root!, "", "file0");

        Assert.Equal(source, result, XNode.DeepEquals);
    }

    [Fact]
    public void Partial()
    {
        using var lf = new LoggerFactory();
        var fuser = Make(lf);

        var source0 = new XDocument(new XElement("partial",
            new XElement("e0")
        ));

        var source1 = new XDocument(new XElement("partial",
            new XElement("e1"),
            new XElement("e2")
        ));

        var expected = new XDocument(FusedBase(
            new XElement("partial",
                new XElement("e0"),
                new XElement("e1"),
                new XElement("e2")
            )
        ));

        var result = new XDocument(FusedBase());

        fuser.Apply(result.Root!, source0.Root!, "", "file0");
        fuser.Apply(result.Root!, source1.Root!, "", "file1");

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Fact]
    public void IndexByFilename()
    {
        using var lf = new LoggerFactory();
        var fuser = Make(lf);

        var source0 = new XDocument(new XElement("indexbyfilename"));
        var source1 = new XDocument(new XElement("indexbyfilename"));

        var expected = new XDocument(FusedBase(
            new XElement("indexbyfilename", FileAttribute("file0")),
            new XElement("indexbyfilename", FileAttribute("file1"))
        ));

        var result = new XDocument(FusedBase());

        fuser.Apply(result.Root!, source0.Root!, "", "file0");
        fuser.Apply(result.Root!, source1.Root!, "", "file1");

        Assert.Equal(expected, result, XNode.DeepEquals);
    }
}
