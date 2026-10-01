namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.IO;
using System.Threading.Tasks;

public sealed class TrackingStream : MemoryStream
{
    public bool IsDisposed { get; private set; }

    public TrackingStream()
        : base(new byte[] { 1, 2, 3, 4, 5 })
    {
    }

    public TrackingStream(byte[] buffer)
        : base(buffer)
    {
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        await base.DisposeAsync();
    }
}
