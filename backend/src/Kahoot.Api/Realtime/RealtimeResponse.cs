using Kahoot.Domain.Common;

namespace Kahoot.Api.Realtime;

public sealed record RealtimeError(string Code, string Description);

public sealed record RealtimeResponse<TValue>(bool Success, TValue? Data, RealtimeError? Error)
{
    public static RealtimeResponse<TValue> Ok(TValue data) => new(true, data, null);

    public static RealtimeResponse<TValue> Failure(Error error) =>
        new(false, default, new RealtimeError(error.Code, error.Description));
}
