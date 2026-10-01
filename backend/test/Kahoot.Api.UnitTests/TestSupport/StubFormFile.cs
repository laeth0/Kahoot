namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public sealed class StubFormFile : IFormFile
{
    public string ContentType { get; set; } = "image/jpeg";

    public string ContentDisposition { get; set; } = "form-data; name=\"file\"; filename=\"test.jpg\"";

    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    public long Length { get; set; } = 1024;

    public string Name { get; set; } = "file";

    public string FileName { get; set; } = "test.jpg";

    public Func<Stream>? OpenReadStreamHandler { get; set; }

    public Exception? OpenReadStreamException { get; set; }

    public bool WasOpenReadStreamCalled { get; private set; }

    public Stream OpenReadStream()
    {
        WasOpenReadStreamCalled = true;

        if (OpenReadStreamException is not null)
        {
            throw OpenReadStreamException;
        }

        if (OpenReadStreamHandler is not null)
        {
            return OpenReadStreamHandler();
        }

        return new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
    }

    public void CopyTo(Stream target)
    {
        using Stream stream = OpenReadStream();
        stream.CopyTo(target);
    }

    public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        await using Stream stream = OpenReadStream();
        await stream.CopyToAsync(target, cancellationToken);
    }
}
