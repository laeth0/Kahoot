namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

public sealed class StubFormFeature : IFormFeature
{
    private IFormCollection _form = FormCollection.Empty;

    public bool HasFormContentType { get; set; } = true;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public IFormCollection Form
    {
        get => _form;
        set => _form = value ?? FormCollection.Empty;
    }

    public Func<CancellationToken, Task<IFormCollection>>? ReadFormAsyncHandler { get; set; }

    public Exception? ExceptionToThrowOnRead { get; set; }

    public bool WasReadFormAsyncCalled { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public IFormCollection ReadForm()
    {
        if (ExceptionToThrowOnRead is not null)
        {
            throw ExceptionToThrowOnRead;
        }

        return Form;
    }

    public async Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
    {
        WasReadFormAsyncCalled = true;
        CapturedCancellationToken = cancellationToken;

        if (ExceptionToThrowOnRead is not null)
        {
            throw ExceptionToThrowOnRead;
        }

        if (ReadFormAsyncHandler is not null)
        {
            return await ReadFormAsyncHandler(cancellationToken);
        }

        return Form;
    }
}
