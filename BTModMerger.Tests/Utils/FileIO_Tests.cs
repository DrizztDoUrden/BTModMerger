using BTModMerger.Abstractions;

namespace BTModMerger.Tests.Utils;

public class FileIO_Tests
{
    [Fact]
    public void Test0()
    {
        var fileio = new FileIO();
        var tmp = Path.GetTempFileName();

        Assert.True(fileio.FileExists(tmp));
        fileio.OpenWriteStream(tmp).Dispose();
        fileio.DeleteFile(tmp);
        Assert.False(fileio.FileExists(tmp));
        fileio.OpenWriteStream(tmp).Dispose();
        fileio.DeleteFile(tmp);
        var tmp2 = Path.Combine(tmp, "test");
        Assert.False(fileio.FileExists(tmp2));
        fileio.OpenWriteStream(tmp2).Dispose();
        fileio.OpenReadStream(tmp2).Dispose();
        fileio.DeleteFile(tmp2);
    }
}
