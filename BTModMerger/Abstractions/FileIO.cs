using BTModMerger.Core.Interfaces;

namespace BTModMerger.Abstractions;

public class FileIO : IFileIO
{
    public Stream OpenStandardInputStream() => Console.OpenStandardInput();
    public Stream OpenStandardOutputStream() => Console.OpenStandardOutput();
    public Stream OpenReadStream(string path) => File.OpenRead(path);

    public Stream OpenWriteStream(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var containingDir = new FileInfo(path).Directory;
            if (!containingDir!.Exists)
                containingDir.Create();
        }

        return File.Create(path);
    }

    public bool FileExists(string outputPath) => File.Exists(outputPath);
    public void DeleteFile(string outputPath) => File.Delete(outputPath);
}
