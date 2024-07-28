using System.Xml.Linq;

namespace BTModMerger.Tests;

using static BTModMerger.Core.BTMMSchema;

internal class CLITestHelpers
{
    public static WrappedMemoryStream MakeEmptyInput(FileIOMocker fileio, string? path = null, bool canReopenAsWrite = false)
    {
        var stream = new WrappedMemoryStream(true, false, reopenAsWrite: canReopenAsWrite);

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

    public static WrappedMemoryStream MakeValidInput(FileIOMocker fileio, string? path = null, XElement? root = null, bool canReopenAsWrite = false)
    {
        var stream = MakeEmptyInput(fileio, path, canReopenAsWrite: canReopenAsWrite);
        new XDocument(root ?? Diff()).Save(stream.stream);
        stream.Position = 0;

        return stream;
    }

    public static WrappedMemoryStream MakeValidOutput(FileIOMocker fileio, string? path = null)
    {
        var stream = new WrappedMemoryStream(false, true);

        if (path is null)
        {
            fileio.Cout = stream;
        }
        else
        {
            fileio.FilesToWrite.Add(path, stream);
        }

        return stream;
    }

    public static void ValidateInput(FileIOMocker fileio, string path, WrappedMemoryStream stream)
    {
        Assert.False(stream.stream.CanRead);
        Assert.Contains(path, fileio.ReadFiles);
        Assert.DoesNotContain(path, fileio.FilesToWrite);
    }

    public static void ValidateOutput(FileIOMocker fileio, string path, WrappedMemoryStream stream)
    {
        Assert.False(stream.stream.CanRead);
        Assert.DoesNotContain(path, fileio.ReadFiles);
        Assert.Contains(path, fileio.FilesToWrite);
    }

    public static void ValidateInOut(FileIOMocker fileio, string path, WrappedMemoryStream stream)
    {
        Assert.False(stream.stream.CanRead);
        Assert.Contains(path, fileio.ReadFiles);
        Assert.Contains(path, fileio.WriteFiles);
    }
}
