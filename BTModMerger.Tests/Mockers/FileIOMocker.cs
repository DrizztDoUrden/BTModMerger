using BTModMerger.Core.Interfaces;

namespace BTModMerger.Tests.Mockers;

internal class FileIOMocker : IFileIO, IDisposable
{
    public class Exception : System.Exception
    {
    }

    public WrappedMemoryStream? Cin { get; set; }
    public WrappedMemoryStream? Cout { get; set; }
    public Dictionary<string, WrappedMemoryStream> FilesToRead { get; } = new();
    public Dictionary<string, WrappedMemoryStream> FilesToWrite { get; } = new();
    public HashSet<string> ReadFiles { get; } = new();
    public HashSet<string> WriteFiles { get; } = new();
    public HashSet<string> ExistingFiles { get; } = new();
    public bool CinOpened { get; private set; } = false;
    public bool CoutOpened { get; private set; } = false;

    Stream IFileIO.OpenStandardInputStream()
    {
        if (CinOpened) throw new Exception();
        CinOpened = true;
        return Cin ?? throw new Exception();
    }

    Stream IFileIO.OpenStandardOutputStream()
    {
        if (CoutOpened) throw new Exception();
        CoutOpened = true;
        return Cout ?? throw new Exception();
    }

    Stream IFileIO.OpenReadStream(string path)
    {
        if (ReadFiles.Contains(path))
            throw new Exception();
        if (!FilesToRead.ContainsKey(path))
            throw new Exception();
        if (!ExistingFiles.Contains(path))
            throw new Exception();

        ReadFiles.Add(path);
        return FilesToRead[path];
    }

    Stream IFileIO.OpenWriteStream(string path)
    {
        if (WriteFiles.Contains(path))
            throw new Exception();

        Stream ret;

        if (FilesToRead.TryGetValue(path, out var stream))
        {
            if (FilesToWrite.ContainsKey(path))
                throw new Exception();

            stream.Reopen();
            ret = stream;
            FilesToRead.Remove(path);
        }
        else
        {
            if (!FilesToWrite.ContainsKey(path))
                throw new Exception();
            ret = FilesToWrite[path];
        }

        ExistingFiles.Add(path);
        WriteFiles.Add(path);

        return ret;
    }

    void IFileIO.DeleteFile(string path)
    {
        if (!ExistingFiles.Contains(path))
            throw new Exception();

        ExistingFiles.Remove(path);
    }

    bool IFileIO.FileExists(string path)
    {
        if (ReadFiles.Contains(path) ||
            WriteFiles.Contains(path))
            throw new Exception();

        return ExistingFiles.Contains(path);
    }

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
