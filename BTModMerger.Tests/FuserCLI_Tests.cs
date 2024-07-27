using System.Text;
using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Tools;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;

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

        Assert.Throws<FileIOMocker.Exception>(() => tool.Apply(["file.xml"], false, false, null, false, true, new(), null));

    }

    [Fact]
    public void FewInputs()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = new MemoryStream();

        new XDocument(new XElement("e")).Save(input);
        input.Position = 0;

        fileio.ExistingFiles.Add("file.xml");
        fileio.FilesToRead.Add("file.xml", input);

        Assert.Throws<InvalidDataException>(() => tool.Apply(["file.xml"], false, false, null, false, true, new(), null));
    }

    [Fact]
    public void InvalidInput()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = new MemoryStream();

        new XDocument(new XElement("e")).Save(input);
        input.Position = 0;

        fileio.ExistingFiles.Add("in0.xml");
        fileio.FilesToRead.Add("in0.xml", input);
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
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

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
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

        var parts =
@"in0.xml
in1.xml";

        var cin = new MemoryStream(Encoding.UTF8.GetBytes(parts));
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
        var output = new MemoryStream();
        fileio.Cout = output;

        tool.Apply(["in0.xml"], true, false, null, false, true, new(), null);

        ValidateInput(fileio, "in0.xml", in0);
        Assert.True(in1.CanRead);
        Assert.True(output.CanRead);
        Assert.True(fileio.CinOpened);
        Assert.True(fileio.CoutOpened);
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

        var conflicts = new MemoryStream();
        var cxml = new FileInfo("c.xml");
        fileio.FilesToWrite.Add(cxml.FullName, conflicts);

        var in0 = MakeValidInput(fileio, "in0.xml");
        var in1 = MakeValidInput(fileio, "in1.xml");
        var output = new MemoryStream();
        fileio.FilesToWrite.Add("out.xml", output);

        tool.Apply(["in0.xml", "in1.xml"], false, false, "out.xml", false, false, new(), new(cxml, @override, deliniarizeConflicts));

        ValidateInput(fileio, "in0.xml", in0);
        ValidateInput(fileio, "in1.xml", in1);
        ValidateOutput(fileio, "out.xml", output);
        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);

        ValidateOutput(fileio, cxml.FullName, conflicts);
    }
}
