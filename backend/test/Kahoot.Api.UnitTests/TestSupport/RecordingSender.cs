namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

public sealed class RecordingSender : ISender
{
    private readonly List<object> _requests = new();
    private readonly List<CancellationToken> _cancellationTokens = new();
    private readonly Queue<Func<object, CancellationToken, Task<object?>>> _responses = new();

    public IReadOnlyList<object> Requests => _requests;

    public IReadOnlyList<CancellationToken> CancellationTokens => _cancellationTokens;

    public void RespondWith<TResponse>(TResponse response)
    {
        _responses.Enqueue((_, _) => Task.FromResult<object?>(response));
    }

    public void RespondWith<TResponse>(Func<TResponse> factory)
    {
        _responses.Enqueue((_, _) => Task.FromResult<object?>(factory()));
    }

    public void RespondWithException(Exception exception)
    {
        _responses.Enqueue((_, _) => Task.FromException<object?>(exception));
    }

    public void RespondWithTask<TResponse>(TaskCompletionSource<TResponse> tcs)
    {
        _responses.Enqueue(async (_, _) => (object?)await tcs.Task);
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        _requests.Add(request);
        _cancellationTokens.Add(cancellationToken);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No response configured for request of type {request.GetType().Name}.");
        }

        Func<object, CancellationToken, Task<object?>> handler = _responses.Dequeue();
        object? result = await handler(request, cancellationToken);

        if (result is TResponse typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException(
            $"Configured response of type {result?.GetType().Name ?? "null"} does not match expected response type {typeof(TResponse).Name}.");
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        throw new NotSupportedException("Non-response Send overload is not used by controller actions.");
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Untyped Send overload is not used by controller actions.");
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming Send is not used by controller actions.");
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Untyped streaming Send is not used by controller actions.");
    }
}
