using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Tools;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;

public class LinearizerCLI_Tests
{
    private class Mocker : ILinearizer
    {
        public XDocument Apply(XDocument input, string inputPath) => input;
    }

    public static ILinearizer MakeMocker() => new Mocker();

    private static LinearizerCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker()
    );

    [Fact]
    public void MissingInput()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        Assert.Throws<FileIOMocker.Exception>(() => tool.Apply("file.xml", null));
    }

    [Fact]
    public void InvalidInput()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = new MemoryStream();

        new XDocument(new XElement("e")).Save(input);
        input.Position = 0;

        fileio.Cin = input;

        Assert.Throws<InvalidDataException>(() => tool.Apply(null, null));
    }

    [Fact]
    public void Valid()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = new MemoryStream();
        var outBuffer = Enumerable.Repeat((byte)0, 1024).ToArray();
        
        var output = new MemoryStream(outBuffer);

        var source = new XDocument(Diff());
        
        source.Save(input);
        input.Position = 0;

        fileio.ExistingFiles.Add("in.xml");
        fileio.FilesToRead.Add("in.xml", input);
        fileio.FilesToWrite.Add("out.xml", output);

        tool.Apply("in.xml", "out.xml");

        Assert.False(input.CanRead);
        Assert.False(output.CanRead);

        var end = Array.IndexOf(outBuffer, (byte)0);
        using var resultStream = new MemoryStream(outBuffer, 0, end);
        var result = XDocument.Load(resultStream);

        Assert.Equal(source, result, XNode.DeepEquals);
        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }
}