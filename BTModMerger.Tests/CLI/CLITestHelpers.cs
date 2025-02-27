﻿using System.Xml.Linq;
using BTModMerger.Tests.Mockers;

namespace BTModMerger.Tests.CLI;

using static BTModMerger.Core.Schema.BTMMSchema;

internal class CLITestHelpers
{
    public static WrappedMemoryStream MakeEmptyInput(FileIOMocker fileio, string? path = null, bool canReopenAsWrite = false)
    {
        var stream = new WrappedMemoryStream(true, false, reopenAsWrite: canReopenAsWrite)
        {
            FileIO = fileio,
            Path = path,
        };

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
        var stream = new WrappedMemoryStream(false, true)
        {
            FileIO = fileio,
            Path = path,
        };

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

    public static void ValidateInput(FileIOMocker fileio, string? path, WrappedMemoryStream stream)
    {
        if (string.IsNullOrEmpty(path))
        {
            Assert.True(fileio.CinOpened);
        }
        else
        {
            Assert.False(stream.stream.CanRead);
            Assert.Contains(path, fileio.ReadFiles);
            Assert.DoesNotContain(path, fileio.FilesToWrite);
        }
    }

    public static void ValidateOutput(FileIOMocker fileio, string? path, WrappedMemoryStream stream)
    {
        if (string.IsNullOrEmpty(path))
        {
            Assert.True(fileio.CoutOpened);
        }
        else
        {
            Assert.False(stream.stream.CanRead);
            Assert.DoesNotContain(path, fileio.ReadFiles);
            Assert.Contains(path, fileio.FilesToWrite);
        }
    }

    public static void ValidateInOut(FileIOMocker fileio, string? path, WrappedMemoryStream stream)
    {
        if (string.IsNullOrEmpty(path))
        {
            Assert.True(fileio.CinOpened);
            Assert.True(fileio.CoutOpened);
        }
        else
        {
            Assert.False(stream.stream.CanRead);
            Assert.Contains(path, fileio.ReadFiles);
            Assert.Contains(path, fileio.WriteFiles);
        }
    }

    public static void ValidateInput(WrappedMemoryStream stream) => ValidateInput(stream.FileIO!, stream.Path, stream);
    public static void ValidateOutput(WrappedMemoryStream stream) => ValidateOutput(stream.FileIO!, stream.Path, stream);
    public static void ValidateInOut(WrappedMemoryStream stream) => ValidateInOut(stream.FileIO!, stream.Path, stream);

    public static void ValidateOutputUnused(FileIOMocker fileio, string? path, WrappedMemoryStream stream)
    {
        if (string.IsNullOrEmpty(path))
        {
            Assert.False(fileio.CoutOpened);
        }
        else
        {
            Assert.True(stream.stream.CanRead);
            Assert.DoesNotContain(path, fileio.ReadFiles);
            Assert.Contains(path, fileio.FilesToWrite);
        }
    }

    public static void ValidateOutputUnused(WrappedMemoryStream stream) => ValidateOutputUnused(stream.FileIO!, stream.Path, stream);
}
