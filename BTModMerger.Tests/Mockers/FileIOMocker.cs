﻿using BTModMerger.Core.Interfaces;

namespace BTModMerger.Tests.Mockers;

internal class FileIOMocker : IFileIO, IDisposable
{
    public class Exception : System.Exception
    {
        public Exception(string message) : base(message) { }
    }

    public WrappedMemoryStream? Cin { get; set; }
    public WrappedMemoryStream? Cout { get; set; }
    public Dictionary<string, WrappedMemoryStream> FilesToRead { get; } = [];
    public Dictionary<string, WrappedMemoryStream> FilesToWrite { get; } = [];
    public Dictionary<string, IEnumerable<string>> ChildFiles { get; } = [];
    public HashSet<string> ReadFiles { get; } = [];
    public HashSet<string> WriteFiles { get; } = [];
    public HashSet<string> ExistingFiles { get; } = [];
    public HashSet<string> Directories { get; } = [];
    public bool CinOpened { get; private set; } = false;
    public bool CoutOpened { get; private set; } = false;

    Stream IFileIO.OpenStandardInputStream()
    {
        if (CinOpened) throw new Exception("Attempt to reopen cin");
        CinOpened = true;
        return Cin ?? throw new Exception("Attempt to open cin unexpectedly");
    }

    Stream IFileIO.OpenStandardOutputStream()
    {
        if (CoutOpened) throw new Exception("Attempt to reopen cout");
        CoutOpened = true;
        return Cout ?? throw new Exception("Attempt to open cout unexpectedly");
    }

    Stream IFileIO.OpenReadStream(string path)
    {
        if (ReadFiles.Contains(path))
            throw new Exception($"Attempt to reopen <{path}>");
        if (!FilesToRead.ContainsKey(path))
            throw new Exception($"Attempt to open unexpected <{path}>");
        if (!ExistingFiles.Contains(path))
            throw new Exception($"Attempt to open non-existent file <{path}>");

        ReadFiles.Add(path);
        return FilesToRead[path];
    }

    Stream IFileIO.OpenWriteStream(string path)
    {
        if (WriteFiles.Contains(path))
            throw new Exception($"Attempt to reopen <{path}>");

        Stream ret;

        if (FilesToRead.TryGetValue(path, out var stream))
        {
            if (FilesToWrite.ContainsKey(path))
                throw new Exception($"Attempt to reopen <{path}> twice");

            stream.Reopen();
            ret = stream;
            FilesToRead.Remove(path);
        }
        else
        {
            if (!FilesToWrite.ContainsKey(path))
                throw new Exception($"Attempt to open unexpected <{path}>");
            ret = FilesToWrite[path];
        }

        ExistingFiles.Add(path);
        WriteFiles.Add(path);

        return ret;
    }

    void IFileIO.DeleteFile(string path)
    {
        if (!ExistingFiles.Contains(path))
            throw new Exception($"Attempt to delete non-existent file <{path}>");

        ExistingFiles.Remove(path);
    }

    bool IFileIO.FileExists(string path)
    {
        if (ReadFiles.Contains(path) ||
            WriteFiles.Contains(path))
            throw new Exception($"Attempt to check existence of file <{path}>, that has already been opened");

        return ExistingFiles.Contains(path);
    }

    bool IFileIO.IsDirectory(string path) => Directories.Contains(path);

    DateTime IFileIO.GetLastWriteTimeUtc(string path) => throw new NotImplementedException();

    IEnumerable<string> IFileIO.GetFiles(string path, string pattern, SearchOption options)
        => ChildFiles.TryGetValue(path, out var result)
            ? result
            : throw new Exception($"Attempt to request child file list from unexpected path <{path}>");

    public void Dispose()
    {
        Cin?.Dispose();
        Cout?.Dispose();

        foreach (var file in FilesToRead)
            file.Value.Dispose();

        foreach (var file in FilesToWrite)
            file.Value.Dispose();
    }
}
