using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Tools;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;

public class ApplierCLI_Tests
{
    private class Mocker : IApplier
    {
        public void Apply(XElement diffElement, XContainer from, XContainer to, string diffPath) { }
    }

    public static IApplier MakeMocker() => new Mocker();

    private static ApplierCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker(),
        LinearizerCLI_Tests.MakeMocker()
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

        Assert.Throws<FileIOMocker.Exception>(() => tool.Apply("base.xml", "mod.xml", "out.xml", false));
    }

    [Fact]
    public void CinCout()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio);
        var mod = MakeValidInput(fileio, "mod.xml", root: new XElement("e"));
        var output = new MemoryStream();
        fileio.Cout = output;

        Assert.Throws<InvalidDataException>(() => tool.Apply(null, "mod.xml", null, false));
    }

    [Fact]
    public void WrongRoot()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio, "base.xml", root: Diff(new XElement("e")));
        var mod = MakeValidInput(fileio, "mod.xml", root: new XElement("e"));
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

        Assert.Throws<InvalidDataException>(() => tool.Apply("base.xml", "mod.xml", "out.xml", false));
    }

    [Theory]
    [InlineData([false])]
    [InlineData([true])]
    public void Valid(bool @override)
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio, "base.xml", root: Diff(new XElement("e")));
        var mod = MakeValidInput(fileio, "mod.xml");
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

        tool.Apply("base.xml", "mod.xml", "out.xml", @override);

        ValidateInput(fileio, "base.xml", @base);
        ValidateInput(fileio, "mod.xml", mod);
        ValidateOutput(fileio, "out.xml", output);
    }

    [Fact]
    public void Fused()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var @base = MakeValidInput(fileio, "base.xml", root: FusedBase(new XElement("e")));
        var mod = MakeValidInput(fileio, "mod.xml");
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

        tool.Apply("base.xml", "mod.xml", "out.xml", false);

        ValidateInput(fileio, "base.xml", @base);
        ValidateInput(fileio, "mod.xml", mod);
    }
}
