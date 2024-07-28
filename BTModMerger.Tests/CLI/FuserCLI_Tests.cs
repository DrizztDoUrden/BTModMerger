using System.Text;
using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Tests.Mockers;
using BTModMerger.Tools;

namespace BTModMerger.Tests.CLI;

using static BTModMerger.Core.BTMMSchema;
using static CLITestHelpers;

public class FuserCLI_Tests
{
    public class FuserMocker : IFuser
    {
        public void Apply(XElement to, XElement part, string dbgPath, string filename) { }
    }

    private static IFuser MakeMocker() => new FuserMocker();

    private static FuserCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker(),
        LinearizerCLI_Tests.MakeMocker(),
        DelinearizerCLI_Tests.MakeMocker(),
        SimplifierCLI_Tests.MakeMocker()
    );

    [Fact]
    public void MissingInput()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        Assert.Throws<FileIOMocker.Exception>(() => tool.Apply(["file.xml"], false, false, null, false, true, new(), null));

    }

    [Fact]
    public void FewInputs()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        MakeValidInput(fileio, "file.xml");

        Assert.Throws<InvalidDataException>(() => tool.Apply(["file.xml"], false, false, null, false, true, new(), null));
    }

    [Fact]
    public void InvalidInput()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        MakeValidInput(fileio, "in0.xml", root: new XElement("e"));
        MakeValidInput(fileio, "in1.xml");

        Assert.Throws<InvalidDataException>(() => tool.Apply(["in0.xml", "in1.xml"], false, false, null, false, true, new(), null));
    }

    [Fact]
    public void DifferentInputs()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        MakeValidInput(fileio, "in0.xml", root: FusedBase());
        MakeValidInput(fileio, "in1.xml");

        Assert.Throws<InvalidDataException>(() => tool.Apply(["in0.xml", "in1.xml"], false, false, null, false, true, new(), null));
    }

    [Fact]
    public void MinimalValid()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var in0 = MakeValidInput(fileio, "in0.xml");
        var in1 = MakeValidInput(fileio, "in1.xml");
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply(["in0.xml", "in1.xml"], false, false, "out.xml", false, true, new(), null);

        ValidateInput(fileio, "in0.xml", in0);
        ValidateInput(fileio, "in1.xml", in1);
        ValidateOutput(fileio, "out.xml", output);
        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }

    [Fact]
    public void PartsFromCin()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var in0 = MakeValidInput(fileio, "in0.xml");
        var in1 = MakeValidInput(fileio, "in1.xml");
        var output = MakeValidOutput(fileio, "out.xml");

        var parts =
@"in0.xml
in1.xml";

        var cin = new WrappedMemoryStream(Encoding.UTF8.GetBytes(parts));
        fileio.Cin = cin;

        cin.Position = 0;

        tool.Apply([], false, true, "out.xml", true, true, new(), null);

        ValidateInput(fileio, "in0.xml", in0);
        ValidateInput(fileio, "in1.xml", in1);
        ValidateOutput(fileio, "out.xml", output);
        Assert.True(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }

    [Fact]
    public void ProcessCin()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var in0 = MakeValidInput(fileio, "in0.xml");
        var in1 = MakeValidInput(fileio);
        var output = MakeValidOutput(fileio);

        tool.Apply(["in0.xml"], true, false, null, false, true, new(), null);

        ValidateInput(fileio, "in0.xml", in0);
        Assert.True(fileio.CinOpened);
        Assert.True(fileio.CoutOpened);
    }

    [Theory]
    [InlineData([false, false])]
    [InlineData([true, false])]
    [InlineData([false, true])]
    [InlineData([true, true])]
    public void Conflicts(bool @override, bool deliniarizeConflicts)
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var cxml = new FileInfo("c.xml");
        var conflicts = MakeValidOutput(fileio, cxml.FullName);

        var in0 = MakeValidInput(fileio, "in0.xml");
        var in1 = MakeValidInput(fileio, "in1.xml");
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply(["in0.xml", "in1.xml"], false, false, "out.xml", false, false, new(), new(cxml, @override, deliniarizeConflicts));

        ValidateInput(fileio, "in0.xml", in0);
        ValidateInput(fileio, "in1.xml", in1);
        ValidateOutput(fileio, "out.xml", output);
        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);

        ValidateOutput(fileio, cxml.FullName, conflicts);
    }

    [Fact]
    public void ConflictsOverride()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var cxml = new FileInfo("c.xml");
        var conflicts = MakeValidInput(fileio, cxml.FullName, canReopenAsWrite: true);

        var in0 = MakeValidInput(fileio, "in0.xml");
        var in1 = MakeValidInput(fileio, "in1.xml");
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply(["in0.xml", "in1.xml"], false, false, "out.xml", false, false, new(), new(cxml, false, false));

        ValidateInput(fileio, "in0.xml", in0);
        ValidateInput(fileio, "in1.xml", in1);
        ValidateOutput(fileio, "out.xml", output);
        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);

        ValidateInOut(fileio, cxml.FullName, conflicts);
    }
}
