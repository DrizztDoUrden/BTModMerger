using BTModMerger.Core.Interfaces;
using BTModMerger.Tests.Mockers;
using BTModMerger.Tools;
using System.Xml.Linq;

namespace BTModMerger.Tests.CLI;

using static BTModMerger.Core.Schema.BTMMSchema;
using static CLITestHelpers;

public class DelinearizerCLI_Tests
{
    private class Mocker : IDelinearizer
    {
        public XDocument Apply(XDocument input, string inputPath) => input;
    }

    public static IDelinearizer MakeMocker() => new Mocker();

    private static DelinearizerCLI Make(IFileIO fileio) => new(
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

        MakeValidInput(fileio, root: new XElement("e"));

        Assert.Throws<InvalidDataException>(() => tool.Apply(null, null));
    }

    [Fact]
    public void Valid()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, "in.xml");
        var output = MakeValidOutput(fileio, "out.xml");

        tool.Apply("in.xml", "out.xml");

        ValidateInput(fileio, "in.xml", input);
        ValidateOutput(fileio, "out.xml", output);

        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }

    [Fact]
    public void InPlace()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, "in.xml");

        tool.Apply("in.xml", null, inPlace: true);

        ValidateInOut(fileio, "in.xml", input);

        Assert.False(fileio.CinOpened);
        Assert.False(fileio.CoutOpened);
    }
}
