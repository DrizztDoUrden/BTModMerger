using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Tests.Mockers;
using BTModMerger.Tools;

namespace BTModMerger.Tests.CLI;

using static BTModMerger.Core.BTMMSchema;
using static CLITestHelpers;

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
        var output = MakeValidOutput(fileio);

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
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply("base.xml", "mod.xml", "out.xml", false, false);

        ValidateInput(fileio, "base.xml", @base);
        ValidateInput(fileio, "mod.xml", mod);

        Assert.True(output.stream.CanRead);
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
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply("base.xml", "mod.xml", "out.xml", @override, delinearize);

        ValidateInput(fileio, "base.xml", @base);
        ValidateInput(fileio, "mod.xml", mod);
        ValidateOutput(fileio, "out.xml", output);
    }
}
