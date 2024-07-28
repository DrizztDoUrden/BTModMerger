namespace BTModMerger.Tests;

public class WrappedMemoryStream(
    bool canRead,
    bool canWrite,
    bool reopenAsRead = false,
    bool reopenAsWrite = false
)
    : Stream
{
    public WrappedMemoryStream(byte[] buffer)
        : this(true, false)
    {
        stream?.Dispose();
        stream = new MemoryStream(buffer);
    }

    public class Exception : System.Exception { }

    public override bool CanRead => canRead;
    public override bool CanWrite => canWrite;

    public override bool CanSeek => stream.CanSeek;
    public override long Length => stream.Length;
    public override long Position { get => stream.Position; set => stream.Position = value; }

    public override void Flush() => stream.Close();
    public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);
    public override void SetLength(long value) => stream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

    public override void Close()
    {
        var reopen = !reopened && (reopenAsRead || reopenAsWrite);

        if (reopen)
        {
            if (reopenAsRead)
            {
                buffer = new byte[stream.Length];
                stream.Read(buffer.AsSpan());
            }
        }

        stream.Close();

        if (!reopen)
            base.Close();
    }

    public void Reopen()
    {
        if (!reopenAsWrite && reopenAsRead) throw new Exception();
        if (reopened) throw new Exception();

        stream = reopenAsRead
            ? new MemoryStream(buffer ?? throw new Exception())
            : new MemoryStream();

        reopened = true;
        buffer = null;
        canWrite = reopenAsWrite;
        canRead = reopenAsRead;
    }

    internal MemoryStream stream = new();
    private bool reopened = false;
    private byte[]? buffer = null;
}
