using System.Xml.Linq;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;

namespace BTModMerger.Tests.Tools;

using static BTMMSchema;

public class Fuser_Tests
{
    public static Fuser Make() => new(
        BTMetadata.Test
    );

    [Fact]
    public void Trivial()
    {
        var fuser = Make();

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
        var fuser = Make();

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
        var fuser = Make();

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
