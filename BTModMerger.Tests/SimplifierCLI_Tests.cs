using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Tools;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;

public class SimplifierCLI_Tests
{
    private class Mocker : ISimplifier
    {
        public XDocument Apply(XDocument input, string inputPath, in ISimplifier.Options options, XElement? conflictsRoot) => input;
    }

    public static ISimplifier MakeMocker() => new Mocker();

    private static SimplifierCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker(),
        DelinearizerCLI_Tests.MakeMocker()
    );

    private static MemoryStream MakeValidInput(FileIOMocker fileio, string? path = null, XElement? root = null)
    {
        var stream = new MemoryStream();
        new XDocument(root ?? Diff()).Save(stream);
        stream.Position = 0;

        if (path is null)
        {
            fileio.Cin = stream;
        }
        else
        {
            fileio.ExistingFiles.Add(path);
            fileio.FilesToRead.Add(path, stream);
        }

        return stream;
    }

    private static void ValidateInput(FileIOMocker fileio, string path, Stream stream)
    {
        Assert.False(stream.CanRead);
        Assert.Contains(path, fileio.ReadFiles);
        Assert.DoesNotContain(path, fileio.FilesToWrite);
    }

    private static void ValidateOutput(FileIOMocker fileio, string path, Stream stream)
    {
        Assert.False(stream.CanRead);
        Assert.DoesNotContain(path, fileio.ReadFiles);
        Assert.Contains(path, fileio.FilesToWrite);
    }

    [Fact]
    public void MissingInput()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        Assert.Throws<FileIOMocker.Exception>(() => tool.Apply("file.xml", null, new(), null));
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

        Assert.Throws<InvalidDataException>(() => tool.Apply(null, null, new(), null));
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

        tool.Apply("in.xml", "out.xml", new(), null);

        Assert.False(input.CanRead);
        Assert.False(output.CanRead);

        var end = Array.IndexOf(outBuffer, (byte)0);
        using var resultStream = new MemoryStream(outBuffer, 0, end);
        var result = XDocument.Load(resultStream);

        Assert.Equal(source, result, XNode.DeepEquals);
        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }

    [Fact]
    public void CinCout()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio);
        var output = new MemoryStream();
        fileio.Cout = output;

        tool.Apply(null, null, new(), null);

        Assert.True(fileio.CinOpened);
        Assert.True(fileio.CoutOpened);
        Assert.True(input.CanRead);
        Assert.True(output.CanRead);
    }

    [Fact]
    public void WrongRoot()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, root: FusedBase());
        var output = new MemoryStream();
        fileio.Cout = output;

        Assert.Throws<InvalidDataException>(() => tool.Apply(null, null, new(), null));
    }

    [Theory]
    [InlineData([false, false])]
    [InlineData([true,  false])]
    [InlineData([false, true])]
    [InlineData([true,  true])]
    public void Conflicts(bool @override, bool deliniarizeConflicts)
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio);
        var output = new MemoryStream();
        fileio.Cout = output;
        var conflicts = new MemoryStream();
        var cxml = new FileInfo("c.xml");
        fileio.FilesToWrite.Add(cxml.FullName, conflicts);

        tool.Apply(null, null, new(), new(cxml, @override, deliniarizeConflicts));

        Assert.True(fileio.CinOpened);
        Assert.True(fileio.CoutOpened);
        Assert.True(input.CanRead);
        Assert.True(output.CanRead);

        ValidateOutput(fileio, cxml.FullName, conflicts);
    }
}