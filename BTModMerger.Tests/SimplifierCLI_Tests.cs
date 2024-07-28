using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Tools;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;
using static CLITestHelpers;

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

        MakeValidInput(fileio, root: new XElement("e"));

        Assert.Throws<InvalidDataException>(() => tool.Apply(null, null, new(), null));
    }

    [Fact]
    public void Valid()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, "in.xml");
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply("in.xml", "out.xml", new(), null);

        ValidateInput(fileio, "in.xml", input);
        ValidateOutput(fileio, "out.xml", output);

        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }

    [Fact]
    public void CinCout()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio);
        var output = MakeValidOutput(fileio);

        tool.Apply(null, null, new(), null);

        Assert.True(fileio.CinOpened);
        Assert.True(fileio.CoutOpened);
    }

    [Fact]
    public void WrongRoot()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, root: FusedBase());
        var output = MakeValidOutput(fileio);

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
        var output = MakeValidOutput(fileio);
        var cxml = new FileInfo("c.xml");
        var conflicts = MakeValidOutput(fileio, cxml.FullName);

        tool.Apply(null, null, new(), new(cxml, @override, deliniarizeConflicts));

        Assert.True(fileio.CinOpened);
        Assert.True(fileio.CoutOpened);

        ValidateOutput(fileio, cxml.FullName, conflicts);
    }

    [Fact]
    public void ConflictsOverride()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var cxml = new FileInfo("c.xml");
        var conflicts = MakeValidInput(fileio, cxml.FullName, canReopenAsWrite: true);

        var input = MakeValidInput(fileio, "inout.xml", canReopenAsWrite: true);

        tool.Apply("inout.xml", null, new(), new(cxml, false, false), true);

        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);

        ValidateInOut(fileio, "inout.xml", input);
        ValidateInOut(fileio, cxml.FullName, conflicts);
    }
}