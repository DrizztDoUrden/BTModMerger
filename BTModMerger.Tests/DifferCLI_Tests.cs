using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Tools;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;

public class DifferCLI_Tests
{
    private class Mocker : IDiffer
    {
        public XDocument Apply(XDocument @base, string basePath, XDocument mod, string modPath, bool alwaysOverride) => @base;
    }

    public static IDiffer MakeMocker() => new Mocker();

    private static DifferCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker(),
        DelinearizerCLI_Tests.MakeMocker()
    );

    private static Stream MakeValidInput(FileIOMocker fileio, string? path = null, XElement? root = null)
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

        Assert.Throws<FileIOMocker.Exception>(() => tool.Apply("base.xml", "mod.xml", "out.xml", false, false));
    }

    [Fact]
    public void CinCout()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio, root: new XElement("e"));
        var mod = MakeValidInput(fileio, "mod.xml", root: new XElement("e"));
        var output = new MemoryStream();
        fileio.Cout = output;

        tool.Apply(null, "mod.xml", null, false, false);

        Assert.True(fileio.CinOpened);
    }

    [Fact]
    public void EmptyResult()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio, "base.xml");
        var mod = MakeValidInput(fileio, "mod.xml");
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);
        fileio.ExistingFiles.Add("out.xml");

        tool.Apply("base.xml", "mod.xml", "out.xml", false, false);

        ValidateInput(fileio, "base.xml", @base);
        ValidateInput(fileio, "mod.xml", mod);

        Assert.True(output.CanRead);
        Assert.DoesNotContain("out.xml", fileio.ReadFiles);
    }


    [Theory]
    [InlineData([false, false])]
    [InlineData([false, true])]
    [InlineData([true, false])]
    [InlineData([true, true])]
    public void Valid(bool @override, bool delinearize)
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio, "base.xml", root: Diff(new XElement("e")));
        var mod = MakeValidInput(fileio, "mod.xml");
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

        tool.Apply("base.xml", "mod.xml", "out.xml", @override, delinearize);

        ValidateInput(fileio, "base.xml", @base);
        ValidateInput(fileio, "mod.xml", mod);
        ValidateOutput(fileio, "out.xml", output);
    }
}
